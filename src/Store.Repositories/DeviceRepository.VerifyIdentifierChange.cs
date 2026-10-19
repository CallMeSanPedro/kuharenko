using System.Collections.Generic;
using Store.Device.Models;

namespace Store.Repositories
{
    public partial interface IDeviceRepository
    {
        IRequest<VerifyIdentifierChangeEntity, List<VerifyIdentifierChangeEntity>> VerifyIdentifierChange(decimal? storeDocumentId, string xml, decimal? filialKod);
    }

    public partial class DeviceRepository
    {
        public IRequest<VerifyIdentifierChangeEntity, List<VerifyIdentifierChangeEntity>> VerifyIdentifierChange(decimal? storeDocumentId, string xml, decimal? filialKod)
        {
            var cmd = new StoreCpe.Store.Device.VerifyIdentifierChange();
            cmd["p_store_document_id"] = storeDocumentId;
            cmd["p_xml"] = xml;
            cmd["p_filial_kod"] = filialKod;
            return cmd;
        }
    }
}
