using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AssistenteInteligente.Models
{
    public class BaseConhecimento
    {
        public string Setor { get; set; } = "";
        public List<string> PalavrasChave { get; set; } = new List<string>();
        public string Regras { get; set; } = "";

        [JsonIgnore]
        public float[] VetorSemantico { get; set; } = Array.Empty<float>();
    }
}