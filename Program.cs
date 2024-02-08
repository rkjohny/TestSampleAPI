using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TestSampleAPI
{
    internal class Program
    {
        public class Person
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }
        }

        public static string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        public static RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        
        public static int MaxDataSet = 1000;
        public static int MaxNumberOfThread = 1;
        public static int RequestPerThread = 1;
        public static int MaxNumberOfRequest = 1;
        public static Person[]? Persons;


        public static string GetNextRandomString(int length)
        {
            StringBuilder result = new();
            
            for (var i = 0; i < length; i++)
            {
                result.Append(Chars[GetNextRandomInt(Chars.Length)]);
            }
            return result.ToString();
        }

        public static int GetNextRandomInt(int upperLimit)
        {
            var data = new byte[1];
            Rng.GetBytes(data);
            return data[0] % upperLimit;
        }
        
        public static void GenerateData()
        {
            Persons = new Person[MaxDataSet];
            for (var i = 0; i < Persons.Length; i++)
            {
                Persons[i] = new Person
                {
                    FirstName = GetNextRandomString(20),
                    LastName = GetNextRandomString(20),
                    Email = GetNextRandomString(20) + "@example.com"
                };
            }
        }

        public static async Task SendRequest(string url)
        {
            // Create HttpClient instance
            using var client = new HttpClient();
            
            // Prepare the request body
            if (Persons != null)
            {
                string requestBody = JsonSerializer.Serialize(Persons[GetNextRandomInt(Persons.Length)]);

                // Create the request content with JSON data
                StringContent content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // Add headers if needed
                //client.DefaultRequestHeaders.Add("Authorization", "Bearer YourAccessToken");
                //client.DefaultRequestHeaders.Add("User-Agent", "YourUserAgent");

                // Send the POST request
                HttpResponseMessage response = await client.PostAsync(url, content);

                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Read and display the response body
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Response Body: " + responseContent);
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                }
            }
        }

        public static async Task ExecuteTestAsync(string url)
        {
            var tasks = new Task[MaxNumberOfRequest];
            
            for (var i = 0; i < MaxNumberOfRequest; i++)
            {
                tasks[i] = Task.Run(() => SendRequest(url));
            }
            Console.WriteLine("All requests have been sent to Url => " + url);
            
            await Task.WhenAll(tasks);
        }
        
        static async Task Main(string[] args)
        {
            const string urlInMemory = "http://localhost:5041/api/Person/in-memory/add-person";
            
            GenerateData();
            await ExecuteTestAsync(urlInMemory);
            Console.WriteLine("testing done to Url => " + urlInMemory);
        }
    }
}
