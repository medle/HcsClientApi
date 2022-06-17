using Hcs.ClientApi.Config;

namespace Hcs.ClientApi.Interfaces
{
    public interface IHcsProvider
    {
        /// <summary>
        /// Конечная точка провайдера
        /// </summary>
        HcsEndPoints EndPoint { get; }
    }
}
