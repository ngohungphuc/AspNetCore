# dotnet-openapi

`dotnet-openapi` is a tool which can be used to manage OpenAPI references within your project.

## Commands

### Add Project
| Short | Long | Description | Example |
|-------|------|-------|---------|
| -v|--verbose | Show verbose output. |dotnet openapi add project *-v* ../Ref/ProjRef.csproj |
| -p|--project | The project to operate on. |dotnet openapi add project *--project .\Ref.csproj* ../Ref/ProjRef.csproj |
| -c|--class-name | The name of the class to generate. |dotnet openapi add project *--class-name YourClass* ../Ref/ProjRef.csproj |

|  Arguments  | Description | Example |
|-------------|-------------|---------|
| source-file | The source to create a reference from. Can be a project file, swagger file or a URL. |dotnet openapi add project *../Ref/ProjRef.csproj* |

### Add File
| Short | Long | Description | Example |
|-------|------|-------|---------|
| -v|--verbose | Show verbose output. |dotnet openapi add file *-v* .\swagger.v1.json |
| -p|--project | The project to operate on. |dotnet openapi add file *--project .\Ref.csproj* .\swagger.v1.json |
| -c|--class-name | The name of the class to generate. |dotnet openapi add file *--class-name YourClass* .\swagger.v1.json |
| -o|--output-file | The file to create a local copy from (only when a url has been supplied as the source-file) |dotnet openapi add file https://contoso.com/swagger.json *--output-file myclient.json* |

|  Arguments  | Description | Example |
|-------------|-------------|---------|
| source-file | The source to create a reference from. Can be a project file, swagger file or a URL. |dotnet openapi add *.\swagger.v1.json* |

### Remove

| Short | Long | Description| Example |
|-------|------|------------|---------|
| -v|--verbose | Show verbose output. |dotnet openapi remove *-v*|
| -p|--project | The project to operate on. |dotnet openapi remove *--project .\Ref.csproj* .\swagger.v1.json |

|  Arguments  | Description| Example |
| ------------|------------|---------|
| source-file | The source to remove the reference to. |dotnet openapi remove *.\swagger.v1.json* |

### Refresh

| Short | Long | Description | Example |
|-------|------|-------------|---------|
| -v|--verbose | Show verbose output. | dotnet openapi refresh *-v* https://contoso.com/swagger.json |
| -p|--project | The project to operate on. | dotnet openapi refresh *--project .\Ref.csproj* https://contoso.com/swagger.json |

|  Arguments  | Description | Example |
| ------------|-------------|---------|
| source-file | The url to refresh the reference from. | dotnet openapi refresh *https://contoso.com/swagger.json* |
