﻿using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    private class Person
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
    }

    public class Request
    {
        public required string TestName { get; init; }
        public required string Url { get; init; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    private const string UrlInMemory = "http://localhost:5041/api/v1/Person/in-memory/add-person";
    private const string UrlPgSql = "http://localhost:5041/api/v1/Person/pg-sql/add-person";
    private const string UrlMySql = "http://localhost:5041/api/v1/Person/my-sql/add-person";
    private const string UrlRedis = "http://localhost:5041/api/v1/Person/redis/add-person";

    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
    private static readonly Random Rnd = new();

    private const int MaxDataSet = 5000;
    private const int TotalNumberOfRequest = 10000;
    private const int NumberOfRequestAtATime = 1000;

    private static Person[]? _persons;

    private static volatile int _totalFailed = 0;

    // Create HttpClient instance
    static readonly HttpClient Client = new ();
    
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

    private static void GenerateData()
    {
        _persons = new Person[MaxDataSet];
        for (var i = 0; i < _persons.Length; i++)
        {
            _persons[i] = new Person
            {
                FirstName = GetNextRandomString(35),
                LastName = GetNextRandomString(35),
                Email =  Guid.NewGuid().ToString() //GetNextRandomString(70)
            };
        }
    }

    private static void Print(Request request)
    {
        Console.WriteLine("*************************************************");
        Console.WriteLine("");

        Console.WriteLine($"Test Name: {request.TestName}");
        Console.WriteLine($"URL: {request.Url}");
        Console.WriteLine($"Total Failed: {_totalFailed}");

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

    private static int GetNextRandomInt(int upperLimit)
    {
        //var data = new byte[1];
        //Rng.GetBytes(data);
        //return data[0] % upperLimit;
        return Rnd.Next(upperLimit);
    }

    private static string GetNextRandomString(int length)
    {
        StringBuilder result = new();
            
        for (var i = 0; i < length; i++)
        {
            result.Append(Chars[GetNextRandomInt(Chars.Length)]);
        }
        return result.ToString();
    }

    private static async Task SendRequest(Request request)
    {
        // Prepare the request body
        if (_persons != null)
        {
            // pick a random person
            var person = _persons[GetNextRandomInt(_persons.Length)];
            var requestBody = JsonSerializer.Serialize(person);

            // Create the request content with JSON data
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // Add headers if needed
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer YourAccessToken");
            //client.DefaultRequestHeaders.Add("User-Agent", "YourUserAgent");

            try
            {
                // Send the POST request
                var response = await Client.PostAsync(request.Url, content);

                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read and display the response body
                    try
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var jsonResponse = JsonNode.Parse(responseContent)!;
                        var resPerson = jsonResponse["person"]!;
                        var resEmail = resPerson["email"]!;
                        
                        if (person.Email != resEmail.GetValue<string>())
                        {
                            Console.WriteLine("Response did not match with input. Input[" + person.Email + " response[" + resEmail.GetValue<string>());
                            Interlocked.Increment(ref _totalFailed);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to parse response. Error: " + ex.Message);
                        Interlocked.Increment(ref _totalFailed);
                    }
                }
                else
                {
                    Console.WriteLine("Error: " + response);
                    Interlocked.Increment(ref _totalFailed);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
                Interlocked.Increment(ref _totalFailed);
            }
        }
    }

    private static async Task ExecuteTestAsync(Request request, int num)
    {
        var tasks = new Task[num];

        for (var i = 0; i < num; i++)
        {
            tasks[i] = Task.Run(() => SendRequest(request));
        }
        await Task.WhenAll(tasks);
    }

    private static async Task ExecuteTest(Request request)
    {
        var n = TotalNumberOfRequest;
        _totalFailed = 0;

        while (n > 0)
        {
            if (n >= NumberOfRequestAtATime)
            {
                await ExecuteTestAsync(request, NumberOfRequestAtATime);
                n -= NumberOfRequestAtATime;
            }
            else
            {
                await ExecuteTestAsync(request, n);
                n = 0;
            }
            Console.Write(".");
            Thread.Sleep(2);
        }
        Console.WriteLine();
        Console.WriteLine("All request have been sent successfully");
    }

    public static Request GetRequest(DbType dbType)
    {
        var request = dbType switch
        {
            DbType.PgSql => new Request { TestName = "PgSql Test:", Url = UrlPgSql, },
            DbType.MySql => new Request { TestName = "MySql Test:", Url = UrlMySql, },
            DbType.InMemory => new Request { TestName = "In Memory Test:", Url = UrlInMemory },
            DbType.Redis => new Request { TestName = "Redis Test:", Url = UrlRedis },
            _ => throw new InvalidEnumArgumentException("Invalid data type")
        };
        return request;
    }
    
    private static async Task Main()
    {
        Client.Timeout = TimeSpan.FromSeconds(30000); // 30 seconds

        Console.WriteLine("*************************************************");
        Console.WriteLine("");
        Console.WriteLine("Generating test data set of size: " + MaxDataSet);
        Console.WriteLine("");

        GenerateData();

        const DbType dbType = DbType.MySql;

        Console.WriteLine("*************************************************");
        Console.WriteLine("");
        Console.WriteLine("Data Generated");
        Console.WriteLine("");
        Console.WriteLine("*************************************************");
        Console.WriteLine("");
        Console.WriteLine("Starting execution of " + TotalNumberOfRequest + " requests: " + dbType);
        Console.WriteLine("");

        var request = GetRequest(dbType);
        
        request.StartTime = DateTime.Now;
        await ExecuteTest(request);
        request.EndTime = DateTime.Now;
        
        Print(request);
    }
}