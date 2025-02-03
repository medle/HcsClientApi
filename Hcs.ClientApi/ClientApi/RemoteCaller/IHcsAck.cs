namespace Hcs.ClientApi.RemoteCaller
{
    public interface IHcsAck
    {
        string MessageGUID { get; set; }
        string RequesterMessageGUID { get; set; }
    }
}
