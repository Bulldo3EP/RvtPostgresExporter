namespace RvtPostgresExporter.Database
{
    public static class ConnectionsSchema
    {
        public const string Json = @"
{
  ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
  ""type"": ""object"",
  ""required"": [""schemaVersion"", ""connections""],
  ""properties"": {
    ""schemaVersion"": { ""type"": ""integer"", ""minimum"": 1 },
    ""defaults"": {
      ""type"": ""object"",
      ""properties"": {
        ""host"": { ""type"": ""string"" },
        ""port"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 65535 },
        ""username"": { ""type"": ""string"" },
        ""timeoutSeconds"": { ""type"": ""integer"", ""minimum"": 1 },
        ""commandTimeoutSeconds"": { ""type"": ""integer"", ""minimum"": 1 },
        ""sslMode"": { ""type"": ""string"", ""enum"": [""Disable"", ""Require"", ""Prefer"", ""VerifyCA"", ""VerifyFull""] },
        ""applicationName"": { ""type"": ""string"" }
      },
      ""additionalProperties"": false
    },
    ""connections"": {
      ""type"": ""array"",
      ""minItems"": 1,
      ""items"": {
        ""type"": ""object"",
        ""required"": [""name"", ""database"", ""password""],
        ""properties"": {
          ""name"": { ""type"": ""string"", ""minLength"": 1 },
          ""host"": { ""type"": ""string"" },
          ""port"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 65535 },
          ""database"": { ""type"": ""string"", ""minLength"": 1 },
          ""username"": { ""type"": ""string"" },
          ""timeoutSeconds"": { ""type"": ""integer"", ""minimum"": 1 },
          ""commandTimeoutSeconds"": { ""type"": ""integer"", ""minimum"": 1 },
          ""sslMode"": { ""type"": ""string"", ""enum"": [""Disable"", ""Require"", ""Prefer"", ""VerifyCA"", ""VerifyFull""] },
          ""applicationName"": { ""type"": ""string"" },
          ""password"": {
            ""type"": ""object"",
            ""required"": [""provider"", ""ref""],
            ""properties"": {
              ""provider"": { ""type"": ""string"", ""enum"": [""WindowsCredentialManager"", ""DpapiFile"", ""EnvironmentVariable""] },
              ""ref"": { ""type"": ""string"", ""minLength"": 1 }
            },
            ""additionalProperties"": false
          }
        },
        ""additionalProperties"": false
      }
    }
  },
  ""additionalProperties"": false
}";
    }
}
