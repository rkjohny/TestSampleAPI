using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TestSampleAPI;

internal class Program
{
    public enum DbType
    {
        PgSql,
        MySql,
        InMemory,
        Redis
    }

    public class Person
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
    }

    public class Request
    {
        public required string TestName { get; set; }
        public required string Url { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public static string urlInMemory = "http://localhost:5041/api/Person/in-memory/add-person";
    public static string urlPgSql = "http://localhost:5041/api/Person/pg-sql/add-person";
    public static string urlMySql = "http://localhost:5041/api/Person/my-sql/add-person";
    public static string urlRedis = "http://localhost:5041/api/Person/redis/add-person";

    public static string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.~!@#$%^&*()_+=-<>,.?;:'|";
    public static RandomNumberGenerator Rng = RandomNumberGenerator.Create();
    public static Random Rnd = new Random();
        
    public static int MaxDataSet = 1000;
    public static int MaxNumberOfRequest = 100;
    public static Person[]? Persons;

    public static volatile int TotalFailed = 0;

    public static string ShuffleString(string stringToShuffle)
    {
        string shuffled;
            
        do
        {
            shuffled = new string(
                stringToShuffle
                    .OrderBy(character => Guid.NewGuid())
                    .ToArray()
            );
        } while (shuffled == stringToShuffle);

        return shuffled;
    }

    public static void GenerateData()
    {
        Persons = new Person[MaxDataSet];
        for (var i = 0; i < Persons.Length; i++)
        {
            Persons[i] = new Person
            {
                FirstName = GetNextRandomString(35),
                LastName = GetNextRandomString(35),
                Email =  Guid.NewGuid().ToString() //GetNextRandomString(70)
            };
            Thread.Sleep(2);
        }
    }

    public static int GetNextRandomInt(int upperLimit)
    {
        //var data = new byte[1];
        //Rng.GetBytes(data);
        //return data[0] % upperLimit;
        return Rnd.Next(upperLimit);
    }
        
    public static string GetNextRandomString(int length)
    {
        StringBuilder result = new();
            
        for (var i = 0; i < length; i++)
        {
            result.Append(Chars[GetNextRandomInt(Chars.Length)]);
        }
        return result.ToString();
    }

    public static async Task SendRequest(Request request)
    {
        // Create HttpClient instance
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30000); // 30 seconds
            
        // Prepare the request body
        if (Persons != null)
        {
            // pick a random person
            var person = Persons[GetNextRandomInt(Persons.Length)];
            string requestBody = JsonSerializer.Serialize(person);

            // Create the request content with JSON data
            StringContent content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // Add headers if needed
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer YourAccessToken");
            //client.DefaultRequestHeaders.Add("User-Agent", "YourUserAgent");

            try
            {
                // Send the POST request
                HttpResponseMessage response = await client.PostAsync(request.Url, content);

                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read and display the response body
                    string responseContent = await response.Content.ReadAsStringAsync();
                    // TODO: verify responseContent
                    //Console.WriteLine("Response Body: " + responseContent);
                }
                else
                {
                    Console.WriteLine("Error: " + response);
                    Interlocked.Increment(ref TotalFailed);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
                Interlocked.Increment(ref TotalFailed);
            }
        }
    }

    public static async Task ExecuteTestAsync(Request request)
    {
        var tasks = new Task[MaxNumberOfRequest];
            
        for (var i = 0; i < MaxNumberOfRequest; i++)
        {
            tasks[i] = Task.Run(() => SendRequest(request));
            Thread.Sleep(10);
        }
            
        await Task.WhenAll(tasks);
    }

    public static async Task Execute(DbType dbType)
    {
        Request? request = null;
        TotalFailed = 0;
            
        switch (dbType)
        {
            case DbType.PgSql:
            {
                request = new Request
                {
                    TestName = "PgSql Test:",
                    Url = urlPgSql,
                };
                break;
            }
            case DbType.MySql:
            {
                request = new Request
                {
                    TestName = "MySql Test:",
                    Url = urlMySql,
                };
                break;
            }
            case DbType.InMemory:
            {
                request = new Request
                {
                    TestName = "In Memory Test:",
                    Url = urlInMemory
                };
                break;
            }
            case DbType.Redis:
            {
                request = new Request
                {
                    TestName = "Redis Test:",
                    Url = urlRedis
                };
                break;
            }
        }

        request!.StartTime = DateTime.Now;
        await ExecuteTestAsync(request!);
        request.EndTime = DateTime.Now;
            
        Print(request);
    }


    public static void Print(Request request)
    {
        Console.WriteLine("*************************************************");
        Console.WriteLine("");

        Console.WriteLine($"Test Name: {request.TestName}");
        Console.WriteLine($"URL: {request.Url}");
        Console.WriteLine($"Total Failed: {TotalFailed}");

        // Calculate the time difference
        if (request is { EndTime: not null, StartTime: not null })
        {
            //var timeDifference = (TimeSpan)(request.EndTime - request.StartTime)!;
            var timeDifference = request.EndTime.Value.Subtract(request.StartTime.Value);
            var differenceInSeconds = (int)timeDifference.TotalSeconds;
            Console.WriteLine($"Time taken in seconds: {differenceInSeconds}");
        }

        Console.WriteLine("");
        Console.WriteLine("*************************************************");
    }
        
    static async Task Main(string[] args)
    {
        GenerateData();

        DbType dbType = DbType.InMemory;

        Console.WriteLine("*************************************************");
        Console.WriteLine("");
        Console.WriteLine("Starting execution of " + MaxNumberOfRequest + " requests: " + dbType);
        Console.WriteLine("");
            
        await Execute(dbType);
    }
}