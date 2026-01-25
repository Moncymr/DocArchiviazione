# RabbitMQ Integration Guide

## Overview

This guide explains how to integrate RabbitMQ message queue for asynchronous document processing in DocN.

## Architecture

```
┌─────────────┐    ┌──────────────┐    ┌─────────────────┐
│  Web API    │───>│  RabbitMQ    │───>│  Worker Service │
│  (Producer) │    │  (Broker)    │    │  (Consumer)     │
└─────────────┘    └──────────────┘    └─────────────────┘
                          │
                          ├─ document.upload.queue (high priority)
                          ├─ document.process.queue (medium priority)
                          ├─ embedding.batch.queue (low priority)
                          └─ document.dlq (dead letter queue)
```

## Prerequisites

- RabbitMQ 3.12+ server
- .NET 8+ SDK
- Management plugin enabled

## Setup RabbitMQ

### Docker Installation

```bash
# Run RabbitMQ with management console
docker run -d \
  --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=docn \
  -e RABBITMQ_DEFAULT_PASS=your_password_here \
  rabbitmq:3.12-management

# Verify it's running
docker logs rabbitmq
```

Access management console at: http://localhost:15672 (user: docn, password: your_password_here)

### Production Cluster Setup

```bash
# docker-compose.yml for RabbitMQ cluster
version: '3.8'
services:
  rabbitmq1:
    image: rabbitmq:3.12-management
    hostname: rabbitmq1
    environment:
      RABBITMQ_ERLANG_COOKIE: 'secret_cookie'
      RABBITMQ_DEFAULT_USER: docn
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq1_data:/var/lib/rabbitmq

  rabbitmq2:
    image: rabbitmq:3.12-management
    hostname: rabbitmq2
    environment:
      RABBITMQ_ERLANG_COOKIE: 'secret_cookie'
    depends_on:
      - rabbitmq1
    volumes:
      - rabbitmq2_data:/var/lib/rabbitmq

  rabbitmq3:
    image: rabbitmq:3.12-management
    hostname: rabbitmq3
    environment:
      RABBITMQ_ERLANG_COOKIE: 'secret_cookie'
    depends_on:
      - rabbitmq1
    volumes:
      - rabbitmq3_data:/var/lib/rabbitmq

volumes:
  rabbitmq1_data:
  rabbitmq2_data:
  rabbitmq3_data:
```

## Implementation

### 1. Install NuGet Package

```bash
cd DocN.Core
dotnet add package RabbitMQ.Client
```

### 2. Create Message Models

Create `DocN.Core/Messages/DocumentUploadMessage.cs`:

```csharp
namespace DocN.Core.Messages;

public class DocumentUploadMessage
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class DocumentProcessMessage
{
    public int DocumentId { get; set; }
    public string ProcessingType { get; set; } = string.Empty; // "ocr", "extraction", "chunking"
    public int RetryCount { get; set; }
    public DateTime QueuedAt { get; set; }
}

public class EmbeddingBatchMessage
{
    public List<int> ChunkIds { get; set; } = new();
    public string EmbeddingModel { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public DateTime QueuedAt { get; set; }
}
```

### 3. Create RabbitMQ Service

Create `DocN.Core/Services/RabbitMQService.cs`:

```csharp
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DocN.Core.Services;

public interface IRabbitMQService
{
    Task PublishAsync<T>(string queueName, T message, byte priority = 5);
    Task<IAsyncEnumerable<T>> ConsumeAsync<T>(string queueName, CancellationToken cancellationToken);
    Task AcknowledgeAsync(ulong deliveryTag);
    Task RejectAsync(ulong deliveryTag, bool requeue = false);
}

public class RabbitMQService : IRabbitMQService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQService> _logger;

    public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:UserName"] ?? "docn",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare queues
        DeclareQueues();
    }

    private void DeclareQueues()
    {
        // Dead letter exchange
        _channel.ExchangeDeclare("docn.dlx", ExchangeType.Direct, durable: true);
        
        // Dead letter queue
        _channel.QueueDeclare(
            queue: "document.dlq",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
        _channel.QueueBind("document.dlq", "docn.dlx", "dlq");

        // Main queues with DLX
        var queues = new[]
        {
            ("document.upload.queue", 10),   // High priority
            ("document.process.queue", 5),   // Medium priority
            ("embedding.batch.queue", 1)     // Low priority
        };

        foreach (var (queueName, maxPriority) in queues)
        {
            var arguments = new Dictionary<string, object>
            {
                { "x-max-priority", maxPriority },
                { "x-dead-letter-exchange", "docn.dlx" },
                { "x-dead-letter-routing-key", "dlq" },
                { "x-message-ttl", 86400000 } // 24 hours
            };

            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments
            );
        }

        _logger.LogInformation("RabbitMQ queues declared successfully");
    }

    public async Task PublishAsync<T>(string queueName, T message, byte priority = 5)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Priority = priority;
        properties.ContentType = "application/json";
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            basicProperties: properties,
            body: body
        );

        _logger.LogDebug("Published message to {Queue}: {Message}", queueName, json);
        await Task.CompletedTask;
    }

    public async Task<IAsyncEnumerable<T>> ConsumeAsync<T>(string queueName, CancellationToken cancellationToken)
    {
        return ConsumeAsyncInternal<T>(queueName, cancellationToken);
    }

    private async IAsyncEnumerable<T> ConsumeAsyncInternal<T>(
        string queueName, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        var tcs = new TaskCompletionSource<T>();

        consumer.Received += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<T>(json);

                if (message != null)
                {
                    tcs.SetResult(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {Queue}", queueName);
                tcs.SetException(ex);
            }
        };

        _channel.BasicConsume(
            queue: queueName,
            autoAck: false,  // Manual acknowledgment
            consumer: consumer
        );

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await tcs.Task;
            tcs = new TaskCompletionSource<T>();
            yield return message;
        }
    }

    public Task AcknowledgeAsync(ulong deliveryTag)
    {
        _channel.BasicAck(deliveryTag, multiple: false);
        return Task.CompletedTask;
    }

    public Task RejectAsync(ulong deliveryTag, bool requeue = false)
    {
        _channel.BasicReject(deliveryTag, requeue);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
```

### 4. Create Worker Service

Create `DocN.Worker/DocumentProcessingWorker.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocN.Worker;

public class DocumentProcessingWorker : BackgroundService
{
    private readonly IRabbitMQService _rabbitMQ;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IRabbitMQService rabbitMQ,
        IServiceProvider serviceProvider,
        ILogger<DocumentProcessingWorker> logger)
    {
        _rabbitMQ = rabbitMQ;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document Processing Worker started");

        // Start multiple consumers in parallel
        var tasks = new[]
        {
            ConsumeDocumentUploads(stoppingToken),
            ConsumeDocumentProcessing(stoppingToken),
            ConsumeEmbeddingBatches(stoppingToken)
        };

        await Task.WhenAll(tasks);
    }

    private async Task ConsumeDocumentUploads(CancellationToken cancellationToken)
    {
        await foreach (var message in _rabbitMQ.ConsumeAsync<DocumentUploadMessage>(
            "document.upload.queue", 
            cancellationToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

                _logger.LogInformation("Processing document upload: {DocumentId}", message.DocumentId);

                // Process document upload
                await documentService.ProcessUploadedDocumentAsync(message.DocumentId);

                // Send to processing queue
                await _rabbitMQ.PublishAsync("document.process.queue", new DocumentProcessMessage
                {
                    DocumentId = message.DocumentId,
                    ProcessingType = "extraction",
                    QueuedAt = DateTime.UtcNow
                });

                _logger.LogInformation("Document {DocumentId} uploaded successfully", message.DocumentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document upload: {DocumentId}", message.DocumentId);
                // Message will be rejected and go to DLQ
            }
        }
    }

    private async Task ConsumeDocumentProcessing(CancellationToken cancellationToken)
    {
        // Similar pattern for document processing
        await Task.CompletedTask;
    }

    private async Task ConsumeEmbeddingBatches(CancellationToken cancellationToken)
    {
        // Similar pattern for embedding batches
        await Task.CompletedTask;
    }
}
```

### 5. Register Services

Update `DocN.Server/Program.cs`:

```csharp
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
builder.Services.AddHostedService<DocumentProcessingWorker>();
```

### 6. Update appsettings.json

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": "5672",
    "UserName": "docn",
    "Password": "your_password_here",
    "VirtualHost": "/"
  }
}
```

## Usage Example

### Publishing Messages

```csharp
public class DocumentController : ControllerBase
{
    private readonly IRabbitMQService _rabbitMQ;

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        // Save file and create document record
        var document = await _documentService.CreateDocumentAsync(file);

        // Publish to queue
        await _rabbitMQ.PublishAsync("document.upload.queue", new DocumentUploadMessage
        {
            DocumentId = document.Id,
            FileName = file.FileName,
            FilePath = document.FilePath,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UploadedAt = DateTime.UtcNow
        }, priority: 10); // High priority

        return Ok(new { documentId = document.Id });
    }
}
```

## Monitoring

### Enable RabbitMQ Prometheus Plugin

```bash
docker exec rabbitmq rabbitmq-plugins enable rabbitmq_prometheus
```

Access metrics at: http://localhost:15692/metrics

### Grafana Dashboard

Import RabbitMQ dashboard: https://grafana.com/grafana/dashboards/10991

## Best Practices

1. **Message Durability:** Always set `persistent: true` for production
2. **Acknowledgment:** Use manual acknowledgment for reliable processing
3. **Dead Letter Queue:** Configure DLQ for failed messages
4. **Priority Queues:** Use priorities for critical operations
5. **Monitoring:** Track queue depth, processing rate, errors
6. **Connection Management:** Use connection pooling and automatic recovery

## Troubleshooting

### Queue Buildup

```bash
# Check queue length
rabbitmqctl list_queues name messages

# Purge queue (development only!)
rabbitmqctl purge_queue document.upload.queue
```

### Connection Issues

```csharp
// Add retry logic
services.AddSingleton<IRabbitMQService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<RabbitMQService>>();
    
    for (int i = 0; i < 5; i++)
    {
        try
        {
            return new RabbitMQService(config, logger);
        }
        catch
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
    
    throw new Exception("Failed to connect to RabbitMQ");
});
```

## References

- [RabbitMQ Official Documentation](https://www.rabbitmq.com/documentation.html)
- [RabbitMQ .NET Client Guide](https://www.rabbitmq.com/dotnet-api-guide.html)
