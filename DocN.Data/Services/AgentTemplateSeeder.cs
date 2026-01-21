using DocN.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocN.Data.Services;

/// <summary>
/// Seeds the database with predefined agent templates for easy setup
/// </summary>
public class AgentTemplateSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AgentTemplateSeeder> _logger;

    public AgentTemplateSeeder(ApplicationDbContext context, ILogger<AgentTemplateSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedTemplatesAsync()
    {
        if (await _context.AgentTemplates.AnyAsync())
        {
            _logger.LogInformation("Agent templates already exist. Skipping seeding.");
            return;
        }

        var templates = new List<AgentTemplate>
        {
            // 1. Simple Q&A Agent - Most common use case
            new AgentTemplate
            {
                Name = "Assistente Domande e Risposte",
                Description = "Risponde alle domande sui tuoi documenti in modo semplice e chiaro. Perfetto per iniziare!",
                Icon = "💬",
                Category = "Generale",
                AgentType = AgentType.QuestionAnswering,
                RecommendedProvider = AIProviderType.Gemini,
                RecommendedModel = "gemini-1.5-flash",
                DefaultSystemPrompt = @"Sei un assistente aziendale esperto e disponibile. Il tuo compito è aiutare gli utenti a trovare informazioni nei loro documenti aziendali.

REGOLE IMPORTANTI:
- Rispondi sempre in modo chiaro e professionale
- Usa le informazioni presenti nei documenti forniti
- Se non trovi la risposta nei documenti, dillo chiaramente
- Cita sempre da quale documento provengono le informazioni
- Sii conciso ma completo

Formato risposta:
1. Rispondi alla domanda
2. Indica i documenti di riferimento usando [Documento N]
3. Se non sei sicuro, suggerisci di cercare ulteriori informazioni",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 5,
                    similarityThreshold = 0.7,
                    temperature = 0.7,
                    maxTokensForContext = 4000,
                    maxTokensForResponse = 1500,
                    useHybridSearch = true
                }),
                ExampleQuery = "Quali sono i documenti relativi al budget 2024?",
                ExampleResponse = "Ho trovato 3 documenti sul budget 2024:\n\n1. Il budget totale previsto è di 1.5M€ [Documento 1: Piano Budget 2024.pdf]\n2. Le spese principali includono...",
                ConfigurationGuide = "Questo agente è ideale per rispondere a domande generiche sui tuoi documenti. Non richiede configurazioni speciali.",
                IsBuiltIn = true,
                IsActive = true
            },

            // 2. Document Summary Agent
            new AgentTemplate
            {
                Name = "Riassuntore Documenti",
                Description = "Crea riassunti chiari e concisi dei tuoi documenti lunghi. Risparmia tempo!",
                Icon = "📝",
                Category = "Analisi",
                AgentType = AgentType.Summarization,
                RecommendedProvider = AIProviderType.Gemini,
                RecommendedModel = "gemini-1.5-flash",
                DefaultSystemPrompt = @"Sei un esperto di sintesi documentale. Il tuo compito è creare riassunti chiari, concisi e ben strutturati.

REGOLE PER IL RIASSUNTO:
- Identifica i punti chiave principali
- Organizza le informazioni in modo logico
- Mantieni il linguaggio professionale ma accessibile
- Evidenzia date, numeri e fatti importanti
- Usa elenchi puntati per migliorare la leggibilità

Struttura del riassunto:
1. Panoramica (2-3 frasi)
2. Punti chiave (elenco)
3. Conclusioni o azioni richieste (se presenti)",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 3,
                    similarityThreshold = 0.6,
                    temperature = 0.5,
                    maxTokensForContext = 6000,
                    maxTokensForResponse = 2000,
                    useHybridSearch = false
                }),
                ExampleQuery = "Riassumi il contratto con il fornitore XYZ",
                ExampleResponse = "**Panoramica**: Contratto di fornitura servizi IT con XYZ per 24 mesi...\n\n**Punti chiave**:\n- Durata: 24 mesi\n- Valore: 50.000€\n- ...",
                ConfigurationGuide = "Usa questo agente quando hai documenti lunghi che vuoi comprendere velocemente.",
                IsBuiltIn = true,
                IsActive = true
            },

            // 3. Legal Document Agent
            new AgentTemplate
            {
                Name = "Assistente Contratti e Documenti Legali",
                Description = "Specializzato nell'analisi di contratti, accordi e documenti legali",
                Icon = "⚖️",
                Category = "Legale",
                AgentType = AgentType.QuestionAnswering,
                RecommendedProvider = AIProviderType.OpenAI,
                RecommendedModel = "gpt-4",
                DefaultSystemPrompt = @"Sei un assistente specializzato in documenti legali e contratti. Hai esperienza nell'analisi di contratti commerciali, accordi e documenti legali aziendali.

REGOLE SPECIFICHE:
- Identifica clausole importanti (durata, penali, responsabilità, risoluzione)
- Evidenzia date di scadenza e milestone
- Segnala obblighi e diritti delle parti
- Usa linguaggio preciso e professionale
- Quando possibile, cita i riferimenti esatti ai paragrafi

ATTENZIONE: Non fornire consulenza legale. Suggerisci sempre di consultare un legale per questioni importanti.",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 3,
                    similarityThreshold = 0.75,
                    temperature = 0.3,
                    maxTokensForContext = 5000,
                    maxTokensForResponse = 2000,
                    useHybridSearch = true
                }),
                ExampleQuery = "Quali sono le clausole di risoluzione anticipata del contratto?",
                ExampleResponse = "Nel contratto sono previste le seguenti condizioni di risoluzione anticipata:\n\n1. Preavviso di 90 giorni [Art. 12.1]...",
                ConfigurationGuide = "Ideale per analizzare contratti, accordi commerciali, NDA e altri documenti legali aziendali.",
                IsBuiltIn = true,
                IsActive = true
            },

            // 4. HR Documents Agent
            new AgentTemplate
            {
                Name = "Assistente Risorse Umane",
                Description = "Trova informazioni su policy HR, benefit, ferie e procedure aziendali",
                Icon = "👥",
                Category = "HR",
                AgentType = AgentType.QuestionAnswering,
                RecommendedProvider = AIProviderType.Gemini,
                RecommendedModel = "gemini-1.5-flash",
                DefaultSystemPrompt = @"Sei un assistente HR che aiuta i dipendenti a trovare informazioni su policy aziendali, benefit, procedure e regolamenti interni.

AREE DI COMPETENZA:
- Policy aziendali e codice etico
- Benefit e welfare aziendale
- Procedure per ferie, permessi, malattia
- Regolamenti interni
- Formazione e sviluppo

COMPORTAMENTO:
- Usa un tono amichevole ma professionale
- Spiega le policy in modo semplice e comprensibile
- Fornisci esempi pratici quando utile
- Indica sempre a chi rivolgersi per ulteriori chiarimenti",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 5,
                    similarityThreshold = 0.7,
                    temperature = 0.6,
                    maxTokensForContext = 4000,
                    maxTokensForResponse = 1500,
                    useHybridSearch = true
                }),
                ExampleQuery = "Come faccio a richiedere le ferie?",
                ExampleResponse = "Per richiedere le ferie devi seguire questa procedura:\n\n1. Compila il modulo richiesta ferie disponibile su...\n2. Invia la richiesta al tuo responsabile...",
                ConfigurationGuide = "Perfetto per gestire domande frequenti dei dipendenti su policy e procedure HR.",
                IsBuiltIn = true,
                IsActive = true
            },

            // 5. Technical Documentation Agent
            new AgentTemplate
            {
                Name = "Assistente Documentazione Tecnica",
                Description = "Specializzato in manuali tecnici, guide operative e documentazione IT",
                Icon = "🔧",
                Category = "Tecnica",
                AgentType = AgentType.QuestionAnswering,
                RecommendedProvider = AIProviderType.Gemini,
                RecommendedModel = "gemini-1.5-pro",
                DefaultSystemPrompt = @"Sei un assistente tecnico esperto che aiuta gli utenti a trovare informazioni in manuali tecnici, guide operative e documentazione IT.

COMPETENZE:
- Procedure tecniche e troubleshooting
- Configurazioni e setup
- Best practices e linee guida
- Specifiche tecniche e requisiti

STILE DI RISPOSTA:
- Fornisci istruzioni passo-passo quando richiesto
- Usa linguaggio tecnico ma chiaro
- Includi warning e note di attenzione
- Suggerisci soluzioni alternative se disponibili",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 7,
                    similarityThreshold = 0.75,
                    temperature = 0.4,
                    maxTokensForContext = 5000,
                    maxTokensForResponse = 2500,
                    useHybridSearch = true
                }),
                ExampleQuery = "Come configuro il server per l'ambiente di produzione?",
                ExampleResponse = "Per configurare il server in produzione, segui questi passaggi:\n\n1. **Prerequisiti**: [Documento: Setup Guide]\n...",
                ConfigurationGuide = "Usa questo agente per manuali tecnici, guide IT e documentazione operativa.",
                IsBuiltIn = true,
                IsActive = true
            },

            // 6. Financial Documents Agent
            new AgentTemplate
            {
                Name = "Assistente Documenti Finanziari",
                Description = "Analizza budget, fatture, rendiconti e documenti finanziari aziendali",
                Icon = "💰",
                Category = "Finanza",
                AgentType = AgentType.DataExtraction,
                RecommendedProvider = AIProviderType.OpenAI,
                RecommendedModel = "gpt-4",
                DefaultSystemPrompt = @"Sei un assistente finanziario che aiuta ad analizzare documenti finanziari aziendali come budget, fatture, rendiconti e report.

COMPETENZE:
- Analisi budget e previsioni
- Estrazione dati da fatture
- Report finanziari e KPI
- Analisi costi e ricavi

FORMATO RISPOSTE:
- Presenta numeri in formato chiaro (usa separatori di migliaia)
- Evidenzia trend e variazioni significative
- Fornisci contesto ai dati numerici
- Usa tabelle quando utile",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 5,
                    similarityThreshold = 0.8,
                    temperature = 0.2,
                    maxTokensForContext = 4000,
                    maxTokensForResponse = 2000,
                    useHybridSearch = true
                }),
                ExampleQuery = "Qual è il budget previsto per il marketing nel Q2?",
                ExampleResponse = "Budget Marketing Q2 2024:\n\n- Budget totale: 150.000€\n- Digital Advertising: 80.000€\n- Eventi: 40.000€\n...",
                ConfigurationGuide = "Ideale per analizzare documenti finanziari e estrarre dati numerici.",
                IsBuiltIn = true,
                IsActive = true
            },

            // 7. Document Comparison Agent
            new AgentTemplate
            {
                Name = "Confronta Documenti",
                Description = "Confronta più documenti per trovare differenze, similitudini e cambiamenti",
                Icon = "🔄",
                Category = "Analisi",
                AgentType = AgentType.Comparison,
                RecommendedProvider = AIProviderType.Gemini,
                RecommendedModel = "gemini-1.5-pro",
                DefaultSystemPrompt = @"Sei un esperto nell'analisi comparativa di documenti. Il tuo compito è identificare differenze, similitudini e cambiamenti tra documenti.

METODO DI LAVORO:
- Identifica differenze chiave e similitudini
- Evidenzia aggiunte, rimozioni e modifiche
- Organizza i risultati in modo strutturato
- Fornisci un riepilogo delle differenze principali

FORMATO OUTPUT:
1. Riepilogo delle differenze principali
2. Dettaglio per sezione/argomento
3. Raccomandazioni (se applicabile)",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 10,
                    similarityThreshold = 0.6,
                    temperature = 0.5,
                    maxTokensForContext = 6000,
                    maxTokensForResponse = 3000,
                    useHybridSearch = true
                }),
                ExampleQuery = "Confronta la versione 1 e 2 del contratto e dimmi cosa è cambiato",
                ExampleResponse = "**Differenze principali tra le versioni:**\n\n**Modifiche sostanziali:**\n- Durata contratto: da 12 a 24 mesi\n- Prezzo: aumentato del 15%\n...",
                ConfigurationGuide = "Usa questo agente per confrontare versioni di documenti, contratti o policy.",
                IsBuiltIn = true,
                IsActive = true
            },

            // 8. Custom Agent Template
            new AgentTemplate
            {
                Name = "Agente Personalizzato",
                Description = "Crea un agente completamente personalizzato per le tue esigenze specifiche",
                Icon = "🎯",
                Category = "Personalizzato",
                AgentType = AgentType.Custom,
                RecommendedProvider = AIProviderType.Gemini,
                RecommendedModel = "gemini-1.5-flash",
                DefaultSystemPrompt = @"Sei un assistente AI configurabile. Segui le istruzioni personalizzate fornite dall'utente.",
                DefaultParametersJson = JsonSerializer.Serialize(new
                {
                    maxDocumentsToRetrieve = 5,
                    similarityThreshold = 0.7,
                    temperature = 0.7,
                    maxTokensForContext = 4000,
                    maxTokensForResponse = 2000,
                    useHybridSearch = true
                }),
                ExampleQuery = "Personalizza le istruzioni e i parametri secondo le tue necessità",
                ExampleResponse = "Le risposte dipenderanno dalle istruzioni personalizzate che configurerai.",
                ConfigurationGuide = "Usa questo template come base per creare un agente completamente personalizzato con comportamenti specifici.",
                IsBuiltIn = true,
                IsActive = true
            }
        };

        _context.AgentTemplates.AddRange(templates);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Seeded {templates.Count} agent templates successfully");
    }
}
