using AIPOC.Exceptions;
using AIPOC.DataSources;
using AIPOC.Models;
using AIPOC.Options;
using Serilog;


namespace AIPOC.DataSources
{
    internal class TestDataSource : DataSource
    {
        public override string Id => "TestDataSource";
        public override string DataFormat => "text_raw";

        public int Delay { get; set; }

        public override async Task InitializeAsync(DataSourceOptions options, ILogger logger)
        {
            await base.InitializeAsync(options, logger);

            if (options?.Settings == null) throw new SettingNotFoundException(logger, nameof(options.Settings));
            string delay = options.Settings!.GetValueOrDefault("Delay", null) ?? throw new SettingNotFoundException(logger, nameof(Delay));
            Delay = int.TryParse(delay, out int delayValue) ? delayValue : throw new SettingWrongTypeException(logger, nameof(Delay), typeof(int).Name);
        }

        public override async Task<bool> PrepareAsync(StartEventData eventData)
        {
            if (!await base.PrepareAsync(eventData)) return false;

            NewData($"Initial {Name} Data");

            return true;
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();

            _ = Task.Run(async () =>
            {
                int i = 0;
                while (IsRunning)
                {
                    NewData($"{Name} Data {i++}");
                    await Task.Delay(Delay); // Simulate data generation delay
                }
            });
        }

        public override Task StopAsync()
        {
            return base.StopAsync();
        }
    }
}
