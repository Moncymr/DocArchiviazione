# API Endpoints for Previously Unexposed Services

This document describes the newly exposed API endpoints for services that were previously not accessible to end users.

## 1. User Activity Tracking API

The `UserActivityController` exposes methods for tracking and retrieving user activity data.

### Endpoints

#### GET /api/user-activity/{userId}
Get user activities for a specific user.

**Parameters:**
- `userId` (path) - The user identifier
- `count` (query, optional) - Number of activities to retrieve (default: 20)

**Response:**
```json
[
  {
    "id": 1,
    "userId": "user123",
    "activityType": "search",
    "description": "Searched for 'contract templates'",
    "documentId": 42,
    "metadata": "{\"query\":\"contract templates\"}",
    "createdAt": "2026-01-25T14:25:57.074Z",
    "document": { ... }
  }
]
```

#### POST /api/user-activity/record
Record a new user activity.

**Request Body:**
```json
{
  "userId": "user123",
  "activityType": "document_view",
  "description": "Viewed document",
  "documentId": 42,
  "metadata": "{\"duration\":\"30s\"}"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Activity recorded successfully"
}
```

#### GET /api/user-activity/{userId}/documents
Get recent document-related activities for a user.

**Parameters:**
- `userId` (path) - The user identifier
- `count` (query, optional) - Number of activities to retrieve (default: 10)

**Response:**
```json
[
  {
    "id": 5,
    "userId": "user123",
    "activityType": "document_access",
    "description": "Accessed document",
    "documentId": 42,
    "createdAt": "2026-01-25T14:25:57.074Z",
    "document": { ... }
  }
]
```

#### GET /api/user-activity/{userId}/statistics
Get activity statistics for a user.

**Parameters:**
- `userId` (path) - The user identifier
- `maxActivities` (query, optional) - Maximum number of recent activities to analyze (default: 500, max: 1000)

**Response:**
```json
{
  "analyzedActivities": 150,
  "limitedTo": 500,
  "note": "All recent activities analyzed.",
  "activityTypeBreakdown": [
    { "activityType": "search", "count": 45 },
    { "activityType": "document_view", "count": 80 },
    { "activityType": "chat", "count": 25 }
  ],
  "documentActivities": 85,
  "recentActivity": "2026-01-25T14:25:57.074Z",
  "mostAccessedDocuments": [
    { "documentId": 42, "count": 15 },
    { "documentId": 38, "count": 12 }
  ]
}
```

**Note:** 
- Statistics are calculated from the most recent activities up to the limit to prevent performance issues with high-activity users.
- `analyzedActivities` reflects the number of activities analyzed (up to `limitedTo`), not necessarily the user's total activity count.
- If `analyzedActivities` equals `limitedTo`, there may be more activities not included in the analysis.

---

## 2. Retrieval Metrics API

The `RetrievalMetricsController` exposes methods for calculating retrieval quality metrics like MRR, NDCG, Precision, Recall, and F1.

### Endpoints

#### POST /api/retrieval-metrics/calculate
Calculate all retrieval metrics for a set of results.

**Request Body:**
```json
{
  "results": [
    {
      "documentId": 1,
      "rank": 1,
      "score": 0.95,
      "isRelevant": true,
      "relevanceGrade": 3
    },
    {
      "documentId": 2,
      "rank": 2,
      "score": 0.82,
      "isRelevant": true,
      "relevanceGrade": 2
    }
  ],
  "totalRelevant": 5
}
```

**Response:**
```json
{
  "mrr": 1.0,
  "ndcg_5": 0.95,
  "ndcg_10": 0.92,
  "precision_5": 0.8,
  "precision_10": 0.7,
  "recall_5": 0.4,
  "recall_10": 0.7,
  "f1_5": 0.533,
  "f1_10": 0.7,
  "totalResults": 10,
  "totalRelevant": 5,
  "measuredAt": "2026-01-25T14:25:57.074Z"
}
```

#### POST /api/retrieval-metrics/mrr
Calculate Mean Reciprocal Rank (MRR).

**Request Body:**
```json
[
  { "documentId": 1, "rank": 1, "score": 0.95, "isRelevant": true, "relevanceGrade": 3 }
]
```

**Response:**
```json
{
  "mrr": 1.0,
  "description": "Mean Reciprocal Rank - measures how quickly relevant results appear"
}
```

#### POST /api/retrieval-metrics/ndcg?k=10
Calculate Normalized Discounted Cumulative Gain (NDCG@K).

**Parameters:**
- `k` (query, optional) - Number of top results to consider (default: 10)

**Request Body:**
```json
[
  { "documentId": 1, "rank": 1, "score": 0.95, "isRelevant": true, "relevanceGrade": 3 }
]
```

**Response:**
```json
{
  "ndcg": 0.92,
  "k": 10,
  "description": "NDCG@10 - measures ranking quality considering relevance scores"
}
```

#### POST /api/retrieval-metrics/precision?k=10
Calculate Precision at K.

**Parameters:**
- `k` (query, optional) - Number of top results to consider (default: 10)

**Request Body:**
```json
[
  { "documentId": 1, "rank": 1, "score": 0.95, "isRelevant": true, "relevanceGrade": 3 }
]
```

**Response:**
```json
{
  "precision": 0.7,
  "k": 10,
  "description": "Precision@10 - relevant docs in top 10 results"
}
```

#### POST /api/retrieval-metrics/recall
Calculate Recall at K.

**Request Body:**
```json
{
  "results": [
    { "documentId": 1, "rank": 1, "score": 0.95, "isRelevant": true, "relevanceGrade": 3 }
  ],
  "k": 10,
  "totalRelevant": 5
}
```

**Response:**
```json
{
  "recall": 0.7,
  "k": 10,
  "description": "Recall@10 - coverage of relevant docs"
}
```

#### POST /api/retrieval-metrics/f1
Calculate F1 score at K.

**Request Body:**
```json
{
  "results": [
    { "documentId": 1, "rank": 1, "score": 0.95, "isRelevant": true, "relevanceGrade": 3 }
  ],
  "k": 10,
  "totalRelevant": 5
}
```

**Response:**
```json
{
  "f1": 0.7,
  "k": 10,
  "description": "F1@10 - harmonic mean of precision and recall"
}
```

**Note:** The API uses different parameter passing styles:
- Simple metrics (MRR, NDCG, Precision) use just the results array in the body, with optional query parameters
- Complex metrics (Recall, F1) use request bodies that include the K value and total relevant count, as these are required for calculation

#### POST /api/retrieval-metrics/summary
Get comprehensive metrics summary with common K values.

**Request Body:** Same as `/calculate`

**Response:**
```json
{
  "meanReciprocalRank": 1.0,
  "ndcg": {
    "at5": 0.95,
    "at10": 0.92
  },
  "precision": {
    "at5": 0.8,
    "at10": 0.7
  },
  "recall": {
    "at5": 0.4,
    "at10": 0.7
  },
  "f1": {
    "at5": 0.533,
    "at10": 0.7
  },
  "totalResults": 10,
  "totalRelevant": 5,
  "measuredAt": "2026-01-25T14:25:57.074Z"
}
```

---

## 3. Extended RAG Quality API

The `RAGQualityController` has been extended with individual RAGAS metric calculation endpoints.

### New Endpoints

#### POST /api/rag-quality/ragas/faithfulness
Calculate faithfulness score (measures if the response is grounded in the provided contexts).

**Request Body:**
```json
{
  "response": "The contract was signed on January 15, 2025.",
  "contexts": [
    "Contract signing date: January 15, 2025",
    "The agreement was executed in New York."
  ]
}
```

**Response:**
```json
{
  "faithfulnessScore": 0.95,
  "description": "Measures if the response is grounded in the provided contexts",
  "timestamp": "2026-01-25T14:25:57.074Z"
}
```

#### POST /api/rag-quality/ragas/relevancy
Calculate answer relevancy score (measures if the response is relevant to the query).

**Request Body:**
```json
{
  "query": "When was the contract signed?",
  "response": "The contract was signed on January 15, 2025."
}
```

**Response:**
```json
{
  "relevancyScore": 0.98,
  "description": "Measures if the response is relevant to the query",
  "timestamp": "2026-01-25T14:25:57.074Z"
}
```

#### POST /api/rag-quality/ragas/context-precision
Calculate context precision (measures if the retrieved contexts are relevant to the query).

**Request Body:**
```json
{
  "query": "When was the contract signed?",
  "contexts": [
    "Contract signing date: January 15, 2025",
    "Unrelated information about weather"
  ],
  "groundTruth": "The contract was signed on January 15, 2025"
}
```

**Note:** The `groundTruth` parameter is optional. When provided, it enables more accurate precision calculation by comparing against expected answers. When omitted, precision is calculated based on semantic relevance alone.

**Response:**
```json
{
  "contextPrecisionScore": 0.75,
  "description": "Measures if the retrieved contexts are relevant to the query",
  "timestamp": "2026-01-25T14:25:57.074Z"
}
```

#### POST /api/rag-quality/ragas/context-recall
Calculate context recall (measures if all relevant context was retrieved).

**Request Body:**
```json
{
  "contexts": [
    "Contract signing date: January 15, 2025"
  ],
  "groundTruth": "The contract was signed on January 15, 2025 in New York"
}
```

**Note:** The `groundTruth` parameter is optional but highly recommended for accurate recall measurement. It helps determine if important information is missing from the retrieved contexts.

**Response:**
```json
{
  "contextRecallScore": 0.65,
  "description": "Measures if all relevant context was retrieved",
  "timestamp": "2026-01-25T14:25:57.074Z"
}
```

#### POST /api/rag-quality/ragas/evaluate-dataset
Evaluate golden dataset for comprehensive testing.

**Request Body:**
```json
{
  "datasetId": "golden-dataset-001"
}
```

**Response:**
```json
{
  "datasetId": "golden-dataset-001",
  "totalSamples": 100,
  "evaluatedSamples": 98,
  "averageScores": {
    "faithfulnessScore": 0.89,
    "answerRelevancyScore": 0.92,
    "contextPrecisionScore": 0.85,
    "contextRecallScore": 0.78,
    "overallRAGASScore": 0.86
  },
  "perSampleScores": { ... },
  "failedSamples": ["sample-45", "sample-67"],
  "evaluatedAt": "2026-01-25T14:25:57.074Z"
}
```

---

## Rate Limiting

- Most endpoints use the `api` rate limiter
- AI-powered endpoints (faithfulness, relevancy, context precision/recall, etc.) use the `ai` rate limiter with stricter limits
- Check server configuration for specific rate limits

## Authentication

All endpoints respect the application's authentication and authorization middleware. Ensure proper authentication headers are included in requests.

## Error Responses

All endpoints return standard error responses:

```json
{
  "error": "Error message describing what went wrong"
}
```

Common HTTP status codes:
- `200 OK` - Successful request
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Authentication required
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error
