using AIPOC.Models;
using AIPOC.Options;
using Serilog;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace AIPOC
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public interface IDataSource
    {
        string Id { get; }
        string Name { get; }
        string DataFormat { get; }

        bool IsActive { get; }

        Task InitializeAsync(DataSourceOptions options, ILogger logger);
        // LatestData should be available before Prepare task completes
        // Return false to not use this data source in the following Start
        Task<bool> PrepareAsync(StartEventData eventData);
        // OnNewData events should not be sent until Start method is called
        Task StartAsync();
        Task StopAsync();

        NewDataEventData? LatestData { get; }
        Action<NewDataEventData> OnNewData { get; set; }
    }
}
