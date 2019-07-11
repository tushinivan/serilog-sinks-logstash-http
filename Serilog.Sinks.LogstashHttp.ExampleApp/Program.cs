using System;

namespace Serilog.Sinks.LogstashHttp.ExampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new LoggerConfiguration()
                .Enrich.WithProperty("app", "def")
                .WriteTo.LogstashHttp(new LogstashHttpSinkOptions()
                {
                    LogstashUri = "http://localhost.ru:8080/",
                    UserName = "user",
                    UserPassword = "password",
                    InlineFields = true,
                    BulkInsert = true
                })
                .CreateLogger();

            logger.Information("Test message");
            logger.Dispose();
        }
    }
}
