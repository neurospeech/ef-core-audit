using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.EFCoreAudit
{

    public interface IAuditContext {

        Task SaveAsync(IEnumerable<AuditItem> items);
        Task SaveAsync(IEnumerable<AuditItem> items, CancellationToken cancellationToken);

    }
}
