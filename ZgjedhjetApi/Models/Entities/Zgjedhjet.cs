using System.ComponentModel.DataAnnotations;
using ZgjedhjetApi.Enums;

namespace ZgjedhjetApi.Models.Entities
{
    public class Zgjedhjet
    {
        [Key]
        public int Id { get; set; }

        public Kategoria Kategoria { get; set; }

        public Komuna Komuna { get; set; }

        public string Qendra_e_Votimit { get; set; } = string.Empty;

        public string VendVotimi { get; set; } = string.Empty;

        public Partia Partia { get; set; }

        public int Vota { get; set; }
    }
}
