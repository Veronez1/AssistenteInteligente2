using AssistenteInteligente.Models;
using AssistenteInteligente.Services; 
using AssistenteInteligente.Builders; 
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Encodings.Web;

namespace AssistenteInteligente
{
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
            var embeddingClient = client.GetEmbeddingClient("text-embedding-3-small");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonBaseConhecimento = await File.ReadAllTextAsync("base_conhecimento.json");
            var bases = JsonSerializer.Deserialize<List<BaseConhecimento>>(jsonBaseConhecimento, options) ?? new List<BaseConhecimento>();

            string jsonChamados = await File.ReadAllTextAsync(Path.Combine("Pendentes", "chamados.json"));
            var chamados = JsonSerializer.Deserialize<List<ChamadoEntrada>>(jsonChamados, options) ?? new List<ChamadoEntrada>();

            Console.WriteLine("Vetorizando a base de conhecimento corporativa...");
            foreach (var baseDoc in bases)
            {
                var embeddingResult = await embeddingClient.GenerateEmbeddingAsync(baseDoc.Regras);
                baseDoc.VetorSemantico = embeddingResult.Value.ToFloats().ToArray();
            }
            Console.WriteLine("Base vetorizada com sucesso!\n");

            var chamadosProcessadosLista = new List<ChamadoProcessado>();

            foreach (ChamadoEntrada chamado in chamados)
            {
                Console.WriteLine($"\nProcessando Ticket ID {chamado.Id}...");

                var chamadoEmbeddingResult = await embeddingClient.GenerateEmbeddingAsync(chamado.Descricao);
                var vetorDoChamado = chamadoEmbeddingResult.Value.ToFloats().ToArray();

                var contextoVencedor = bases
                    .Select(b => new
                    {
                        Base = b,
                        Similaridade = CalculadoraVetorial.CalcularSimilaridadeCosseno(b.VetorSemantico, vetorDoChamado)
                    })
                    .OrderByDescending(x => x.Similaridade)
                    .FirstOrDefault();

                Console.WriteLine($"[Busca Vetorial] Contexto achado: {contextoVencedor?.Base.Setor} (Score: {contextoVencedor?.Similaridade:F4})");

                string regraRecuperada = contextoVencedor != null && contextoVencedor.Similaridade > 0.1f
                                         ? contextoVencedor.Base.Regras
                                         : "Nenhuma regra específica encontrada.";

                string systemPromptText = GeradorDePrompt.GerarInstrucoesDoSistema(regraRecuperada);
                var systemMessage = new SystemChatMessage(systemPromptText);

                string userPromptText = GeradorDePrompt.GerarMensagemUsuario(chamado.Descricao, chamado.Id);
                var userMessage = new UserChatMessage(userPromptText);

                List<ChatMessage> mensagens = new List<ChatMessage> { systemMessage, userMessage };

                var response = await chatClient.CompleteChatAsync(mensagens);
                string jsonRetorno = response.Value.Content[0].Text.Trim();

                if (jsonRetorno.StartsWith("```json")) jsonRetorno = jsonRetorno.Replace("```json", "").Replace("```", "").Trim();

                try
                {
                    var resultado = JsonSerializer.Deserialize<ChamadoProcessado>(jsonRetorno, options) ?? new ChamadoProcessado();
                    if (resultado.Id == 0) resultado.Id = chamado.Id;

                    chamadosProcessadosLista.Add(resultado);
                    Console.WriteLine($"[GPT] Triagem concluída (Setor: {resultado.setor} | Urgência: {resultado.urgencia}).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erro ao converter JSON: " + ex.Message);
                }
            }

            string diretorioDestino = Path.Combine(Directory.GetCurrentDirectory(), "Processados");
            if (!Directory.Exists(diretorioDestino)) Directory.CreateDirectory(diretorioDestino);

            string nomeArquivo = $"chamados_triados_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string caminhoCompleto = Path.Combine(diretorioDestino, nomeArquivo);
            string jsonFinal = JsonSerializer.Serialize(chamadosProcessadosLista, options);

            await File.WriteAllTextAsync(caminhoCompleto, jsonFinal);
            Console.WriteLine($"\n=== TRIAGEM FINALIZADA. Arquivo salvo em: {nomeArquivo} ===");
        }
    }
}