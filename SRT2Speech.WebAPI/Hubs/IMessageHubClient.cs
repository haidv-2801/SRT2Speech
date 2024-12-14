namespace SRT2Speech.WebAPI.Hubs
{
    public interface IMessageHubClient
    {
        Task SendMessage(string message);
    }
}
