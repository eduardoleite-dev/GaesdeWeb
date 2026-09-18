# GaesdeWeb - Template Web .NET 10

Este repositório contém uma aplicação base Blazor Server em .NET 10, preparada para ser instalada como um template personalizado do .NET. A base inclui Home, Login, sessão de autenticação e integração com endpoints públicos e protegidos por JWT.

---

## 1. Pré-requisitos

Verifique se o SDK do .NET 10 está instalado:

```powershell
dotnet --version
```

O projeto utiliza a API GAESDE configurada em `appsettings.json`:

- `POST api/Auth/login`: autenticação.
- `GET api/HelloWorld/publico`: endpoint público.
- `GET api/HelloWorld/privado`: endpoint protegido por Bearer token.

Para executar o deploy, também é necessário ter o Azure CLI instalado e autenticado:

```powershell
az login
```

---

## 2. Estrutura do projeto base

O projeto e a configuração do template ficam diretamente na raiz deste repositório:

```text
GaesdeWeb/
├── .template.config/
│   └── template.json
├── Pages/
├── Services/
├── Models/
├── wwwroot/
├── GaesdeWeb.csproj
├── Program.cs
├── deploy.ps1
└── README.md
```

A sessão mantém `token`, `expiresAt`, `username` e `userId` em `sessionStorage`. Senhas não são persistidas.

---

## 3. Criar e validar a aplicação base

Restaure as dependências e confirme que o projeto compila:

```powershell
dotnet restore
dotnet build
```

Execute localmente:

```powershell
dotnet run
```

---

## 4. Criar um template próprio do .NET (`dotnet new`)

O arquivo `.template.config/template.json` transforma este repositório em um template reconhecido pelo comando `dotnet new`.

A propriedade `sourceName` define o nome usado na substituição automática:

```json
{
  "identity": "GaesdeWeb.Template",
  "name": "Arquitetura Base Web .NET 10",
  "shortName": "baseweb",
  "sourceName": "GaesdeWeb"
}
```

Ao gerar uma aplicação com outro nome, o .NET substitui `GaesdeWeb` pelo novo nome nos arquivos, no projeto e nos namespaces.

### Instalar o template localmente

Abra o terminal na raiz deste repositório, onde estão `.template.config` e `GaesdeWeb.csproj`, e execute:

```powershell
dotnet new install .
```

Confirme que o template foi registrado:

```powershell
dotnet new list baseweb
```

### Criar uma nova aplicação a partir do template

Saia da pasta do template ou informe um diretório de destino separado. Depois execute:

```powershell
dotnet new baseweb -n MinhaAplicacaoWeb
Set-Location MinhaAplicacaoWeb
dotnet restore
dotnet build
dotnet run
```

Também é possível definir diretamente o diretório de saída:

```powershell
dotnet new baseweb -n MinhaAplicacaoWeb -o .\MinhaAplicacaoWeb
```

O resultado será uma aplicação independente com o projeto renomeado para `MinhaAplicacaoWeb`.

### Desinstalar o template local

Na raiz do repositório:

```powershell
dotnet new uninstall .
```

---

## 5. Deploy no Azure App Service

O script `deploy.ps1` publica a aplicação, cria `deploy.zip` e envia o pacote para um Azure App Service Linux.

Execute na raiz da aplicação que será publicada:

```powershell
.\deploy.ps1 -ResourceGroup "MeuResourceGroup" -AppName "minha-webapp"
```

O script recebe:

- `ResourceGroup`: grupo de recursos do Azure.
- `AppName`: nome do Azure App Service.

Exemplo completo:

```powershell
az login
.\deploy.ps1 `
  -ResourceGroup "MinhaAplicacao_group" `
  -AppName "minha-aplicacao-web"
```

O script detecta automaticamente o arquivo `.csproj`, executa `dotnet publish` em Release e cria o pacote de deploy.

---

## Resumo dos comandos

| Comando | Descrição |
|---------|-----------|
| `dotnet restore` | Restaura as dependências do projeto |
| `dotnet build` | Compila a aplicação base |
| `dotnet run` | Executa a aplicação localmente |
| `dotnet new install .` | Instala este repositório como template |
| `dotnet new list baseweb` | Lista e confirma o template instalado |
| `dotnet new baseweb -n MinhaAplicacaoWeb` | Cria uma nova aplicação usando o template |
| `dotnet new uninstall .` | Remove a instalação local do template |
| `.\deploy.ps1 -ResourceGroup ... -AppName ...` | Publica no Azure App Service |

Assim, a `GaesdeWeb` pode ser usada como ponto de partida para novas aplicações Web .NET 10 sem copiar arquivos manualmente.
