using System.Collections.Generic;
using Store.Device.Models;

namespace Store.Repositories
{
    public partial interface IDeviceRepository
    {
        IRequest<VerifyIdentifierChange, TList> SelectVerifyIdentifierChangeXml<TList>(decimal? storeDocumentId, string xmlData, decimal? filialKod)
            where TList : class, ICollection<VerifyIdentifierChange>, new();

        IRequest AddIdentifierChangeToDocXml(decimal? storeDocumentId, string xmlData);
    }

    public partial class DeviceRepository
    {
        public IRequest<VerifyIdentifierChange, TList> SelectVerifyIdentifierChangeXml<TList>(decimal? storeDocumentId, string xmlData, decimal? filialKod)
            where TList : class, ICollection<VerifyIdentifierChange>, new()
        {
            var cmd = new StoreCpe.Store.Device.VerifyIdentifierChange<VerifyIdentifierChange>();
            cmd.Params.PStoreDocumentId = storeDocumentId;
            cmd.Params.PXml = xmlData;
            cmd.Params.PFilialKod = filialKod;
            return new DatumNodeQueryRequest<VerifyIdentifierChange, TList>(cmd);
        }

        public IRequest AddIdentifierChangeToDocXml(decimal? storeDocumentId, string xmlData)
        {
            var cmd = new StoreCpe.Store.Device.AddIdentifierChangeToDoc();
            cmd.Params.PStoreDocumentId = storeDocumentId;
            cmd.Params.PXml = xmlData;
            return new DatumNodeBatchRequest(cmd);
        }
    }
}
