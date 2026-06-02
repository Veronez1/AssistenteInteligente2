using AssistenteInteligente;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization; 
using System.Threading.Tasks;
using System.Text.Encodings.Web;

public class ChamadoEntrada
{
    public int Id { get; set; }
    public string Descricao { get; set; } = "";
}

public class BaseConhecimento
{
    public string Setor { get; set; } = "";
    public List<string> PalavrasChave { get; set; } = new List<string>();
    public string Regras { get; set; } = "";

    // NOVO: Essa propriedade guarda a matemática da frase. 
    // O [JsonIgnore] diz pro C# não tentar ler isso do seu arquivo base_conhecimento.json
    [JsonIgnore]
    public float[] VetorSemantico { get; set; } = Array.Empty<float>();
}

public class ChamadoProcessado
{
    public int Id { get; set; }
    public string setor { get; set; } = "";
    public string urgencia { get; set; } = "";
    public string resposta { get; set; } = "";
    public string regras { get; set; } = "";
}

class Program
{
    // === O CORAÇÃO DO BANCO VETORIAL (Matemática Pura) ===
    // Esta função compara dois arrays de números e diz o quão próximos eles estão (de -1.0 a 1.0)
    static float CalcularSimilaridadeCosseno(ReadOnlySpan<float> vetorA, ReadOnlySpan<float> vetorB)
    {
        float produtoEscalar = 0, magnitudeA = 0, magnitudeB = 0;
        for (int i = 0; i < vetorA.Length; i++)
        {
            produtoEscalar += vetorA[i] * vetorB[i];
            magnitudeA += vetorA[i] * vetorA[i];
            magnitudeB += vetorB[i] * vetorB[i];
        }
        return (float)(produtoEscalar / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB)));
    }

    static async Task Main()
    {
        Console.WriteLine("Iniciando Sistema RAG com Busca Vetorial Semântica...\n");

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Chave da API não encontrada.");
            return;
        }

        var client = new OpenAIClient(apiKey);
        var chatClient = client.GetChatClient("gpt-4o-mini");

        // NOVO: Cliente específico para gerar os Embeddings (Transformar texto em matemática)
        var embeddingClient = client.GetEmbeddingClient("text-embedding-3-small");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 1. Carrega os arquivos JSON
        string jsonBaseConhecimento = await File.ReadAllTextAsync("base_conhecimento.json");
        var bases = JsonSerializer.Deserialize<List<BaseConhecimento>>(jsonBaseConhecimento, options) ?? new List<BaseConhecimento>();

        string jsonChamados = await File.ReadAllTextAsync(Path.Combine("Pendentes", "chamados.json"));
        var chamados = JsonSerializer.Deserialize<List<ChamadoEntrada>>(jsonChamados, options) ?? new List<ChamadoEntrada>();

        // 2. [FASE DE INDEXAÇÃO VETORIAL] Transformando a Base de Conhecimento em Embeddings
        Console.WriteLine("Vetorizando a base de conhecimento corporativa...");
        foreach (var baseDoc in bases)
        {
            // Pede pra OpenAI transformar o texto da regra em um array de 1536 números-
            var embeddingResult = await embeddingClient.GenerateEmbeddingAsync(baseDoc.Regras);
            baseDoc.VetorSemantico = embeddingResult.Value.ToFloats().ToArray();
        }
        Console.WriteLine("Base vetorizada com sucesso!\n");

        var chamadosProcessadosLista = new List<ChamadoProcessado>();

        // 3. [FASE DE RAG E BUSCA SEMÂNTICA] Processando os chamados
        foreach (ChamadoEntrada chamado in chamados)
        {
            Console.WriteLine($"\nProcessando Ticket ID {chamado.Id}...");

            // Passo A: Transforma o problema do usuário em matemática
            var chamadoEmbeddingResult = await embeddingClient.GenerateEmbeddingAsync(chamado.Descricao);
            var vetorDoChamado = chamadoEmbeddingResult.Value.ToFloats().ToArray();

            // Passo B: O PRÉ-FILTRO VETORIAL (Busca a regra com o significado mais próximo)
            var contextoVencedor = bases
                .Select(b => new
                {
                    Base = b,
                    Similaridade = CalcularSimilaridadeCosseno(b.VetorSemantico, vetorDoChamado)
                })
                .OrderByDescending(x => x.Similaridade)
                .FirstOrDefault();

            Console.WriteLine($"[Busca Vetorial] Contexto achado: {contextoVencedor?.Base.Setor} (Score: {contextoVencedor?.Similaridade:F4})");

            // Passo C: RAG - Injeta APENAS a regra vencedora no System Prompt
            string regraRecuperada = contextoVencedor != null && contextoVencedor.Similaridade > 0.1f
                                     ? contextoVencedor.Base.Regras
                                     : "Nenhuma regra específica encontrada.";

            string systemPromptText = PromptBuilder.GerarInstrucoesDoSistema(regraRecuperada);
            var systemMessage = new SystemChatMessage(systemPromptText);

            string userPromptText = PromptBuilder.GerarMensagemUsuario(chamado.Descricao, chamado.Id);
            var userMessage = new UserChatMessage(userPromptText);

            List<ChatMessage> mensagens = new List<ChatMessage> { systemMessage, userMessage };

            // Passo D: O LLM gera a resposta baseada no contexto injetado
            var response = await chatClient.CompleteChatAsync(mensagens);
            string jsonRetorno = response.Value.Content[0].Text.Trim();

            if (jsonRetorno.StartsWith("```json"))
            {
                jsonRetorno = jsonRetorno.Replace("```json", "").Replace("```", "").Trim();
            }

            try
            {
                var resultado = JsonSerializer.Deserialize<ChamadoProcessado>(jsonRetorno, options) ?? new ChamadoProcessado();
                if (resultado != null)
                {
                    // Força o ID do chamado para não perder a referência
                    resultado.Id = chamado.Id;
                    chamadosProcessadosLista.Add(resultado);
                    Console.WriteLine($"[GPT] Triagem concluída (Setor: {resultado.setor} | Urgência: {resultado.urgencia}).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao converter JSON: " + ex.Message);
            }
        }

        // 4. Salvar os resultados processados
        string diretorioDestino = Path.Combine(Directory.GetCurrentDirectory(), "Processados");
        if (!Directory.Exists(diretorioDestino)) Directory.CreateDirectory(diretorioDestino);

        string nomeArquivo = $"chamados_triados_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string caminhoCompleto = Path.Combine(diretorioDestino, nomeArquivo);
        string jsonFinal = JsonSerializer.Serialize(chamadosProcessadosLista, options);

        await File.WriteAllTextAsync(caminhoCompleto, jsonFinal);
        Console.WriteLine($"\n=== TRIAGEM FINALIZADA. Arquivo salvo em: {nomeArquivo} ===");
    }
}