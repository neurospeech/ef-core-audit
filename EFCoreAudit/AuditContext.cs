using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NeuroSpeech.EFCoreAudit
{

    public class AuditService : DbContext {

        private readonly IServiceProvider service;
        private readonly Func<IServiceProvider, UserInfo> userInfoProvider;

        public AuditService(IServiceProvider service, Func<IServiceProvider, UserInfo> userInfoProvider)
        {
            this.service = service;
            this.userInfoProvider = userInfoProvider;

            
        }

        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Unspecified
        };

        public async Task<IEnumerable<AuditItem>> StartTrackingAsync(DbContext context)
        {
            List<AuditItem> items = new List<AuditItem>();

            foreach (var entry in context.ChangeTracker.Entries())
            {

                if (entry.Entity is IIgnoreAudit)
                    continue;


                Type type = entry.Entity.GetType();

                var item = new AuditItem
                {
                    TableName = type.Name,
                    PrimaryKey = GetKey(entry),
                    Entry = entry
                };

                switch (entry.State)
                {
                    case EntityState.Deleted:
                        item.Operation = AuditOperation.Removed;
                        break;
                    case EntityState.Modified:
                        var values = await entry.GetDatabaseValuesAsync();
                        var cv = entry.CurrentValues;
                        var modified = entry.Members.Where(x => x.IsModified).ToList();
                        item.OldValues = SerializeObject(values, cv, modified);
                        item.NewValues = SerializeObject(cv, values, modified);
                        item.Operation = AuditOperation.Modified;
                        break;
                    case EntityState.Added:
                        item.Operation = AuditOperation.Added;
                        item.NewValues = SerializeObject(entry.CurrentValues, null);
                        break;
                    default:
                        continue;
                }



                items.Add(item);

                //var fkeys = entry.Metadata
                //    .GetNavigations()
                //    .Where(x => !x.IsCollection())
                //    .Where(x => x.ForeignKey.DeclaringEntityType.ClrType == type)
                //    .Select(x => (
                //        name: x.PropertyInfo.PropertyType.Name,
                //        navigation: x.ForeignKey.DependentToPrincipal.Name,
                //        property: x.ForeignKey.Properties.FirstOrDefault(),
                //        x: x
                //    ));

                //foreach (var fk in fkeys) {

                //    var v = fk.property.PropertyInfo.GetValue(entry.Entity)?.ToString();
                //    if (v == null)
                //        continue;

                //    var parent = new AuditItem {
                //        TableName = fk.name,
                //        PrimaryKey = v,
                //        UserInfo = item.UserInfo,
                //        IPAddress = item.IPAddress,
                //        Notes = item.Notes,
                //        Operation = AuditOperation.Modified,
                //        Parent = item,
                //        Timestamp = now
                //    };
                //    items.Add(parent);
                //}
            }

            return items;
        }

        private string SerializeObject(
            PropertyValues currentValues,
            PropertyValues ignoreValues,
            List<MemberEntry> modifiedList = null)
        {
            //var values = currentValues.Properties.Where(x => !x.IsKey()).Select(p => new KVP
            //{
            //    Key = p.Name,
            //    Value = currentValues[p]
            //}).Where(x=>x.Value != null)
            //.ToList();

            //return JsonConvert.SerializeObject(values);
            var obj = new Newtonsoft.Json.Linq.JObject();
            foreach (var p in currentValues.Properties.Where(x => !x.IsKey()))
            {
                var v = currentValues[p];
                if (v == null)
                    continue;
                if (ignoreValues != null)
                {
                    if (!modifiedList.Any(x => x.Metadata.Name == p.Name))
                        continue;
                    var iv = ignoreValues[p];
                    if (iv != null)
                    {
                        if (Object.Equals(v, iv))
                            continue;
                    }
                }
                obj.Add(p.Name, Newtonsoft.Json.Linq.JToken.FromObject(v));
            }
            return obj.ToString(Formatting.Indented);

        }

        private string GetKey(EntityEntry entry)
        {
            if (!entry.IsKeySet)
                return null;
            return string.Join(",", entry.Metadata.GetKeys().SelectMany(x => x.Properties)
                .Select(x => x.PropertyInfo.GetValue(entry.Entity).ToString()));
        }
    }


    public abstract class AuditContext : DbContext, IAuditContext
    {

        private readonly ISystemClock dateTimeService;

        public AuditContext(
            DbContextOptions<AuditContext> options,
            ISystemClock dateTimeService)
            : base(options)
        {
            this.dateTimeService = dateTimeService;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AuditItem>()
                .HasIndex(nameof(AuditItem.TableName), nameof(AuditItem.Timestamp), nameof(AuditItem.PrimaryKey))
                .HasName("IX_AuditHistory_Tables");
        }

        public DbSet<AuditItem> AuditHistory { get; set; }



        Task IAuditStorage.SaveAsync(IEnumerable<AuditItem> items)
        {

            var now = dateTimeService.UtcNow.UtcDateTime;


            foreach (var item in items)
            {
                var userInfo = GetUserInfo();
                item.UserInfo = userInfo?.Username;
                item.IPAddress = userInfo?.IPAddress;
                item.Notes = userInfo?.UserAgent;
                item.Timestamp = now;
                if (item.Operation == AuditOperation.Added)
                {
                    item.PrimaryKey = GetKey(item.Entry);
                }
            }

            AuditHistory.AddRange(items);

            return SaveChangesAsync();
        }


    }
}
