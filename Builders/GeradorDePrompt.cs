namespace AssistenteInteligente.Builders
{
    public static class GeradorDePrompt
    {
        // 1. Esta parte será enviada como SystemChatMessage (Fica em Cache)
        public static string GerarInstrucoesDoSistema(string regrasGerais)
        {
            return $@"Você é o sistema inteligente de triagem de chamados corporativos.
            Sua função é definir o próximo passo para a resolução do chamado ESTRITAMENTE com base nas regras de conhecimento abaixo.
            
            Instrução de estilo para o campo ""resposta"":
            - Seja direto, prático e use verbos no imperativo (tom de comando/ação).
            - NUNCA narre a ação (ex: ""Oriente o atendente a..."", ""O sistema deve dizer que..."").
            - Vá direto ao ponto (ex: ""Limpar o cache do aplicativo CRM"", ""Isolar a área imediatamente e acionar manutenção"").
            
            --- REGRAS DE CONHECIMENTO ---
            {regrasGerais}
            ------------------------
            
            Responda APENAS com um JSON válido (sem blocos markdown), exatamente neste formato:
            {{
              ""id"": 0,
              ""setor"": """",
              ""regras"": """",
              ""urgencia"": """",
              ""resposta"": """"
            }}
            Os setores válidos são: [TI, MANUTENCAO, VENDAS, QUALIDADE]";
        }

        public static string GerarMensagemUsuario(string descricaoChamado, int IdChamado)
        {
            return $"Chamado recebido: Id: {IdChamado} \"{descricaoChamado}\"";
        }
    }
}