using AssistenteInteligente;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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
    public string setor { get; set; } = "";
    public string urgencia { get; set; } = "";
    public string resposta { get; set; } = "";
    public string regras { get; set; } = "";
}

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Iniciando...");

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Chave da API não encontrada nas variáveis de ambiente.");
            return;
        }
        var client = new OpenAIClient(apiKey);
        var chatClient = client.GetChatClient("gpt-4o-mini");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string jsonBaseConhecimento = await File.ReadAllTextAsync("base_conhecimento.json");

        var bases = JsonSerializer.Deserialize<List<BaseConhecimento>>(jsonBaseConhecimento, options)
                    ?? new List<BaseConhecimento>();
        string jsonChamados = await File.ReadAllTextAsync("chamados.json");
        var chamados = JsonSerializer.Deserialize<List<ChamadoEntrada>>(jsonChamados, options)
                   ?? new List<ChamadoEntrada>();



        foreach (ChamadoEntrada chamado in chamados)
        {
            Console.WriteLine($"\n Processando Ticket ID {chamado.Id}...");

            string descricaoLower = chamado.Descricao.ToLower();

            var contextoVencedor = bases
              .Select(baseConhecimento => new
              {
                  Base = baseConhecimento,
                  Pontuacao = baseConhecimento.PalavrasChave.Count(palavraChave => descricaoLower.Contains(palavraChave))
              })
              .OrderByDescending(candidato => candidato.Pontuacao)
              .FirstOrDefault();

            string regrasParaInjetar = contextoVencedor != null && contextoVencedor.Pontuacao > 0
                                       ? "Setor: " + contextoVencedor.Base.Setor + "Regras: " + contextoVencedor.Base.Regras
                                       : "Nenhuma regra específica encontrada. Analisar com base no bom senso corporativo.";

            Console.WriteLine($"Contexto recuperado com sucesso: Setor {contextoVencedor?.Base.Setor ?? "N/A"} Regras {contextoVencedor?.Base.Regras ?? "N/A"} (Pontos: {contextoVencedor?.Pontuacao})");

            string promptEnxuto = PromptBuilder.GerarPromptTriagem(regrasParaInjetar, chamado.Descricao);

            var response = await chatClient.CompleteChatAsync(promptEnxuto);
            string jsonRetorno = response.Value.Content[0].Text.Trim();
            Console.WriteLine($"IA Ação: {jsonRetorno}");
            try
            {
                var resultado = JsonSerializer.Deserialize<ChamadoProcessado>(jsonRetorno, options) ?? new ChamadoProcessado();

                Console.WriteLine($"IA Setor Mapeado: {resultado.setor} | Urgência: {resultado.urgencia}");
                Console.WriteLine($"IA Regra Usada:   {resultado.regras}");
                Console.WriteLine($"IA Ação:          {resultado.resposta}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao converter JSON: " + ex.Message);
            }
        }
        Console.WriteLine("\n === TRIAGEM FINALIZADA ===");
    }
}