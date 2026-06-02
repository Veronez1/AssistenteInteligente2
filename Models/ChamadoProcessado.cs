namespace AssistenteInteligente.Models
{
    public class ChamadoProcessado
    {
        public int Id { get; set; }
        public string setor { get; set; } = "";
        public string urgencia { get; set; } = "";
        public string resposta { get; set; } = "";
        public string regras { get; set; } = "";
    }
}