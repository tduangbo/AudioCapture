using AIPOC.Models;
using AIPOC.Options;
using Serilog;
using AIPOC;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace AIPOC.DataSources
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public abstract class DataSource : IDataSource
    {
        public abstract string Id { get; }
        public virtual string Name => Options?.Name ?? "";
        public abstract string DataFormat { get; }

        public bool IsActive { get; protected set; }
        public bool IsRunning { get; protected set; }

        public NewDataEventData? LatestData { get; protected set; }
        public Action<NewDataEventData> OnNewData { get; set; } = (eventData) => { };

        protected ILogger? Logger { get; private set; }

        public virtual Task InitializeAsync(DataSourceOptions options, ILogger logger)
        {
            Options = options;
            Logger = logger.ForContext("Type", options.Type).ForContext("Name", options.Name);
            return Task.CompletedTask;
        }

        public virtual Task<bool> PrepareAsync(StartEventData data)
        {
            if (Options?.Modes?.Contains(data.Mode) != true && Options?.Modes?.Contains(Constants.Mode_All) != true) { return Task.FromResult(false); }
            
            IsActive = true;

            Logger?.Information("Preparing {Type} {Name} for {ConfirmationNumber} with mode {Mode}", Id, Name, data.ConfirmationNumber, data.Mode);
            ConfirmationNumber = data.ConfirmationNumber;
            Mode = data.Mode;

            return Task.FromResult(true);
        }

        public virtual Task StartAsync()
        {
            if (!IsActive) return Task.CompletedTask;
            Logger?.Information("Starting {Type} {Name} for {ConfirmationNumber} with mode {Mode}", Id, Name, ConfirmationNumber, Mode);
            IsRunning = true;
            return Task.CompletedTask;
        }

        public virtual Task StopAsync()
        {
            if (!IsActive) return Task.CompletedTask;
            Logger?.Information("Stopping {Type} {Name} for {ConfirmationNumber} with mode {Mode}", Id, Name, ConfirmationNumber, Mode);
            IsRunning = false;
            IsActive = false;
            return Task.CompletedTask;
        }

        protected DataSourceOptions? Options { get; set; }
        protected string? ConfirmationNumber { get; set; }
        protected string? Mode { get; set; }
        
        protected void NewData(object newData, NewDataEventData? newEventData = null)
        {
            if (!IsActive) {
                Logger?.Information("{Type} {Name} cannot create new data for {ConfirmationNumber} with mode {Mode} because it is not active", Id, Name, ConfirmationNumber, Mode);
                return;
            }
            Logger?.Information("{Type} {Name} Creating new data for {ConfirmationNumber} with mode {Mode}", Id, Name, ConfirmationNumber, Mode);

            newEventData ??= new NewDataEventData
            {
                Data = newData,
                Format = DataFormat,
                Name = Name,
                Timestamp = DateTime.Now,
                Type = Id,
            };

            LatestData = newEventData;
            if (IsRunning) OnNewData?.Invoke(newEventData);
        }
    }
}