using System.Collections.Generic;
using Store.Device.Models;

namespace Store.Repositories
{
    public partial interface IDeviceRepository
    {
        IRequest<VerifyIdentifierChange, TList> SelectVerifyIdentifierChangeXml<TList>(decimal? storeDocumentId, string xmlData, decimal? filialKod)
            where TList : class, ICollection<VerifyIdentifierChange>, new();

        IRequest AddIdentifierChangeToDocXml(decimal? storeDocumentId, string xmlData, decimal? ownershipId, decimal? devConditionId, decimal? sosttechKod);
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

        public IRequest AddIdentifierChangeToDocXml(decimal? storeDocumentId, string xmlData, decimal? ownershipId, decimal? devConditionId, decimal? sosttechKod)
        {
            var cmd = new StoreCpe.Store.Device.AddIdentifierChangeToDoc();
            cmd.Params.PStoreDocumentId = storeDocumentId;
            cmd.Params.PXml = xmlData;
            cmd.Params.POwnershipId = ownershipId;
            cmd.Params.PDevConditionId = devConditionId;
            cmd.Params.PSosttechKod = sosttechKod;
            return new DatumNodeBatchRequest(cmd);
        }
    }
}
