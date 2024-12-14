namespace SRT2Speech.Core.Models
{
    public class MessageModel
    {
        public int ID { get; set; }
        public string Message { get; set; } = default!;
        public string File { get; set; } = default!;
        public DateTime TimeStamp { get; set; } = DateTime.Now;
    }
}
