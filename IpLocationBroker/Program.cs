using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public class ProviderStats
{
    private readonly object _lock = new();
    private readonly Queue<(DateTime Timestamp, bool IsError, long ResponseTime)> _history = new();
    private int _requestsInCurrentMinute = 0;
    private DateTime _currentMinute = DateTime.UtcNow;

    public int ErrorCountLast5Min { get; private set; }
    public double AvgResponseTimeLast5Min { get; private set; }
    public int RequestsInLastMinute => _requestsInCurrentMinute;

    public string Name { get; set; }
    public string UrlTemplate { get; set; }
    public int MaxRequestsPerMinute { get; set; }

    public void LogRequest(bool isError, long responseTime)
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if ((now - _currentMinute).TotalMinutes >= 1)
            {
                _requestsInCurrentMinute = 0;
                _currentMinute = now;
            }
            _requestsInCurrentMinute++;

            _history.Enqueue((now, isError, responseTime));
            CleanOldEntries();
            RecalculateStats();
        }
    }

    private void CleanOldEntries()
    {
        while (_history.Count > 0 && (DateTime.UtcNow - _history.Peek().Timestamp).TotalMinutes > 5)
        {
            _history.Dequeue();
        }
    }

    private void RecalculateStats()
    {
        var valid = _history.ToList();
        ErrorCountLast5Min = valid.Count(x => x.IsError);
        AvgResponseTimeLast5Min = valid.Count > 0 ? valid.Average(x => x.ResponseTime) : double.MaxValue;
    }

    public bool CanAcceptRequest()
    {
        lock (_lock)
        {
            return _requestsInCurrentMinute < MaxRequestsPerMinute;
        }
    }
}

public class LocationBrokerService
{
    private readonly List<ProviderStats> _providers;
    private readonly HttpClient _httpClient = new();

    public LocationBrokerService(List<ProviderStats> providers)
    {
        _providers = providers;
    }

    public async Task<string> GetLocationAsync(string ip)
    {
        var provider = SelectBestProvider();

        if (provider == null)
            throw new Exception("No available providers");

        var url = provider.UrlTemplate.Replace("{ip}", ip);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await _httpClient.GetAsync(url);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                provider.LogRequest(true, sw.ElapsedMilliseconds);
                throw new Exception("Provider returned error");
            }

            var result = await response.Content.ReadAsStringAsync();
            provider.LogRequest(false, sw.ElapsedMilliseconds);
            return result;
        }
        catch
        {
            sw.Stop();
            provider.LogRequest(true, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private ProviderStats SelectBestProvider()
    {
        return _providers
            .Where(p => p.CanAcceptRequest())
            .OrderBy(p => p.ErrorCountLast5Min)
            .ThenBy(p => p.AvgResponseTimeLast5Min)
            .FirstOrDefault();
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var providers = new List<ProviderStats>
        {
            new ProviderStats
            {
                Name = "ipinfo",
                UrlTemplate = "https://ipinfo.io/{ip}/json",
                MaxRequestsPerMinute = 7
            },
            new ProviderStats
            {
                Name = "ip-api",
                UrlTemplate = "http://ip-api.com/json/{ip}",
                MaxRequestsPerMinute = 8
            }
        };

        var broker = new LocationBrokerService(providers);

        // Dummy IPs for testing
        var testIps = new List<string>
        {"8.8.8.8",         // Google DNS
    "1.1.1.1",         // Cloudflare DNS
    "208.67.222.222",  // OpenDNS
    "9.9.9.9",         // Quad9 DNS
    "185.199.108.153", // GitHub Pages
    "216.58.217.206",  // Google.com
    "104.244.42.1",    // Twitter.com
    "151.101.1.69",    // Stack Overflow
    "172.217.9.206",   // Google.com (alternate)
    "151.101.65.69",   // Stack Overflow (alternate)
    "104.16.123.96",   // Cloudflare site
    "23.22.63.216",    // AWS
    "192.30.255.112"   // GitHub
        };

        foreach (var ip in testIps)
        {
            try
            {
                var location = await broker.GetLocationAsync(ip);
                Console.WriteLine($"IP: {ip}");
                Console.WriteLine($"Response:\n{location}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed for {ip}: {ex.Message}");
            }

            await Task.Delay(1000); // To respect rate limits
        }

        Console.WriteLine("Done. Press any key to exit...");
        Console.ReadKey();
    }
}
