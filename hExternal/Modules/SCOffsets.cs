using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using hExternal.Features;
using hExternal.modules;
using hExternal.Modules;

namespace hExternal.modules
{
    public class SCOffsets
    {
        private static SCOffsets? _instance;
        private static readonly object _lock = new object();
        private readonly HttpClient _client = new HttpClient();
        private JsonElement offsets;
        private JsonElement clientDll;
        private JsonElement engineDll;
        private JsonElement buttons;

        private SCOffsets()
        {
            try
            {

                var offsetsTask = LoadJson("https://raw.githubusercontent.com/a2x/cs2-dumper/refs/heads/main/output/offsets.json");
                var clientDllTask = LoadJson("https://raw.githubusercontent.com/a2x/cs2-dumper/refs/heads/main/output/client_dll.json");
                var engineDllTask = LoadJson("https://raw.githubusercontent.com/a2x/cs2-dumper/refs/heads/main/output/engine2_dll.json");
                var buttonsTask = LoadJson("https://raw.githubusercontent.com/a2x/cs2-dumper/refs/heads/main/output/buttons.json");


                //var offsetsTask = LoadJson("https://raw.githubusercontent.com/lordcreations/cs2-dumper/refs/heads/main/output/offsets.json");
                //var clientDllTask = LoadJson("https://raw.githubusercontent.com/lordcreations/cs2-dumper/refs/heads/main/output/client_dll.json");
                //var engineDllTask = LoadJson("https://raw.githubusercontent.com/lordcreations/cs2-dumper/refs/heads/main/output/engine2_dll.json");
                //var buttonsTask = LoadJson("https://raw.githubusercontent.com/lordcreations/cs2-dumper/refs/heads/main/output/buttons.json");

                Task.WaitAll(offsetsTask, clientDllTask, engineDllTask, buttonsTask);

                offsets = offsetsTask.Result;
                clientDll = clientDllTask.Result;
                engineDll = engineDllTask.Result;
                buttons = buttonsTask.Result;

                Log.Info("Offsets and related data loaded successfully.");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load offsets or related data: {e.Message}");
                Environment.Exit(1);
            }
        }

        public static SCOffsets Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SCOffsets();
                        }
                    }
                }
                return _instance;
            }
        }

        private async Task<JsonElement> LoadJson(string url)
        {
            var response = await _client.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);
            return jsonDoc.RootElement;
        }

        public int Buttons(string key)
        {
            try
            {
                return buttons.GetProperty("client.dll").GetProperty(key).GetInt32();
            }
            catch (Exception e)
            {
                Log.Error($"Button key '{key}' not found: {e.Message}");
                Environment.Exit(1);
                return 0;
            }
        }

        public int Offset(string key)
        {
            try
            {
                return offsets.GetProperty("client.dll").GetProperty(key).GetInt32();
            }
            catch (Exception e)
            {
                Log.Error($"Offset key '{key}' not found: {e.Message}");
                Environment.Exit(1);
                return 0;
            }
        }


        public int Engine2dll(string key)
        {
            try
            {
                return offsets.GetProperty("engine2.dll").GetProperty(key).GetInt32();
            }
            catch (Exception e)
            {
                Log.Error($"Offset key '{key}' not found: {e.Message}");
                return 0;
            }
        }


        public int Client(string className, string key)
        {
            try
            {
                return clientDll
                    .GetProperty("client.dll")
                    .GetProperty("classes")
                    .GetProperty(className)
                    .GetProperty("fields")
                    .GetProperty(key)
                    .GetInt32();
            }
            catch (Exception e)
            {
                Log.Error($"Client data not found for class '{className}' and key '{key}': {e.Message}");
                return 0;
            }
        }

        public int Engine(string key)
        {
            try
            {
                return engineDll.GetProperty("engine.dll").GetProperty(key).GetInt32();
            }
            catch (Exception e)
            {
                Log.Error($"Engine data key '{key}' not found: {e.Message}");
                return 0;
            }
        }
    }
}