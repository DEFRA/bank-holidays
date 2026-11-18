# Introduction 
TODO: Give a short introduction of your project. Let this section explain the objectives or the motivation behind this project. 

# Getting Started
TODO: Guide users through getting your code up and running on their own system. In this section you can talk about:
1.	Installation process
2.	Software dependencies
3.	Latest releases
4.	API references

# Build and Test
TODO: Describe and show how to build your code and run the tests. 

# Container

The multi-stage [Dockerfile](./Dockerfile) follows the Defra .NET container
standard ([defra-docker-dotnetcore](https://github.com/DEFRA/defra-docker-dotnetcore)):
a `development` stage (`defradigital/dotnetcore-development`) restores, tests
and publishes the app; a `production` stage (`defradigital/dotnetcore`) runs
it as the non-root `dotnet` user with write permissions removed. It listens
on HTTP 8080.

```powershell
docker build --target production -t bank-holidays:local .
```

# Contribute
TODO: Explain how other users and developers can contribute to make your code better. 

If you want to learn more about creating good readme files then refer the following [guidelines](https://www.visualstudio.com/en-us/docs/git/create-a-readme). You can also seek inspiration from the below readme files:
- [ASP.NET Core](https://github.com/aspnet/Home)
- [Visual Studio Code](https://github.com/Microsoft/vscode)
- [Chakra Core](https://github.com/Microsoft/ChakraCore)