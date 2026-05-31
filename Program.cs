using AssistenteInteligente;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    static async Task Main()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Chave da API não encontrada.");
            return;
        }

        var client = new OpenAIClient(apiKey);
        var chatClient = client.GetChatClient("gpt-4o-mini");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string jsonBaseConhecimento = await File.ReadAllTextAsync("base_conhecimento.json");
        var bases = JsonSerializer.Deserialize<List<BaseConhecimento>>(jsonBaseConhecimento, options)
                    ?? new List<BaseConhecimento>();

        string jsonChamados = await File.ReadAllTextAsync(
            Path.Combine("Pendentes", "chamados.json")
        );
        var chamados = JsonSerializer.Deserialize<List<ChamadoEntrada>>(jsonChamados, options)
                       ?? new List<ChamadoEntrada>();

        string regrasGerais = string.Join("\n", bases.Select(contexto => $"Setor: {contexto.Setor} | Regras: {contexto.Regras}"));

        string systemPromptText = PromptBuilder.GerarInstrucoesDoSistema(regrasGerais);

        var systemMessage = new SystemChatMessage(systemPromptText);
        var chamadosProcessadosLista = new List<ChamadoProcessado>();
        foreach (ChamadoEntrada chamado in chamados)
        {
            Console.WriteLine($"\n Processando Ticket ID {chamado.Id}...");

            string userPromptText = PromptBuilder.GerarMensagemUsuario(chamado.Descricao, chamado.Id);
            var userMessage = new UserChatMessage(userPromptText);

            List<ChatMessage> mensagens = new List<ChatMessage>
            {
                systemMessage,
                userMessage 
            };

            var response = await chatClient.CompleteChatAsync(mensagens);
            string jsonRetorno = response.Value.Content[0].Text.Trim();

            if (jsonRetorno.StartsWith("```json"))
            {
                jsonRetorno = jsonRetorno.Replace("```json", "").Replace("```", "").Trim();
            }
            try
            {
                var resultado = JsonSerializer.Deserialize<ChamadoProcessado>(jsonRetorno, options)
                                ?? new ChamadoProcessado();
                if (resultado != null)
                {
                    chamadosProcessadosLista.Add(resultado);
                    Console.WriteLine($"[ID {chamado.Id}] Triagem concluída (Setor: {resultado.setor}).");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao converter JSON: " + ex.Message);
            }
        }
        string diretorioDestino = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Processados"
        );

        if (!Directory.Exists(diretorioDestino))
        {
            Directory.CreateDirectory(diretorioDestino);
        }

        string nomeArquivo = $"chamados_triados_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string caminhoCompleto = Path.Combine(diretorioDestino, nomeArquivo);
        string jsonFinal = JsonSerializer.Serialize(chamadosProcessadosLista, options);
        await File.WriteAllTextAsync(caminhoCompleto, jsonFinal);
    }
}

