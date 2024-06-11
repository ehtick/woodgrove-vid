using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoodgroveDemo.Models
{

    public class Status
    {
        public string RequestStateId { get; set; }
        public string RequestStatus { get; set; }
        public string Message { get; set; }
        public string ID { get; set; }
        public string JsonPayload { get; set; }
        public string Flow { get; set; }
        public string Scenario { get; set; }
        public string IndexedClaimValue { get; set; }
        public DateTime StartTime { get; set; }
        public List<StatusCallbacks> History { get; set; } = new List<StatusCallbacks>();

        public Status(string scenario, string flow) : this()
        {
            this.Scenario = scenario;
            this.Flow = flow;
        }

        public Status()
        {
            StartTime = DateTime.Now;
            History.Add(new StatusCallbacks("Started", "00:00:00", ""));
            ID = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Serialize this object into a string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Deserialize a JSON string into Status object
        /// </summary>
        /// <param name="JsonString">The JSON string to be loaded</param>
        /// <returns></returns>
        public static Status Parse(string JsonString)
        {
            return JsonSerializer.Deserialize<Status>(JsonString);
        }

        public string CalculateExecutionTime()
        {
            TimeSpan ts = DateTime.Now.Subtract(StartTime);
            return String.Format("{0:00}:{1:00}:{2:00}",
                    ts.Hours, ts.Minutes, ts.Seconds);
        }

        public double CalculateExecutionSeconds()
        {
            TimeSpan ts = DateTime.Now.Subtract(StartTime);
            return ts.TotalSeconds;
        }

        public void AddHistory(string message, string timing)
        {
            this.History.Add(new StatusCallbacks(message, timing, "Check out the HTTP request tab"));
        }
        public void AddHistory(string message, string timing, string payload)
        {
            this.History.Add(new StatusCallbacks(message, timing, payload));
        }
    }

        public class StatusCallbacks
    {
        public string Timing { get; set; }
        public string Message { get; set; }
        public string Payload { get; set; }

        public StatusCallbacks(string message, string timing, string payload)
        {
            this.Message = message;
            this.Timing = timing;
            this.Payload = payload;
        }
    }
}