namespace Hcs.ClientApi.Interfaces
{
    public interface IHcsAck
    {
        string MessageGUID { get; set; }
        string RequesterMessageGUID { get; set; }

    }
}
