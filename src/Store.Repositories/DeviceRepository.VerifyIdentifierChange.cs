using System.Collections.Generic;
using Store.Device.Models;

namespace Store.Repositories
{
    public partial interface IDeviceRepository
    {
        IRequest<VerifyIdentifierChangeEntity, TList> SelectVerifyIdentifierChangeXml<TList>(decimal? storeDocumentId, string xmlData, decimal? filialKod)
            where TList : class, ICollection<VerifyIdentifierChangeEntity>, new();
    }

    public partial class DeviceRepository
    {
        public IRequest<VerifyIdentifierChangeEntity, TList> SelectVerifyIdentifierChangeXml<TList>(decimal? storeDocumentId, string xmlData, decimal? filialKod)
            where TList : class, ICollection<VerifyIdentifierChangeEntity>, new()
        {
            var cmd = new StoreCpe.Store.Device.VerifyIdentifierChange<VerifyIdentifierChangeEntity>();
            cmd.Params.PStoreDocumentId = storeDocumentId;
            cmd.Params.PXml = xmlData;
            cmd.Params.PFilialKod = filialKod;
            return new DatumNodeQueryRequest<VerifyIdentifierChangeEntity, TList>(cmd);
        }
    }
}
