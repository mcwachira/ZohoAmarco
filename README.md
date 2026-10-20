# ZohoAmarco

**A C#/.NET integration service connecting Zoho Books to a Tremol G03 fiscal control unit.**

ZohoAmarco is being developed as a production-oriented integration for retrieving invoice data from Zoho Books, processing it through a locally connected fiscal control unit using the documented Tremol SDK/protocol, and recording the resulting fiscalization details against the corresponding Zoho Books invoice.

> **Development status:** Initial development. Device compatibility, fiscalization workflows, error recovery, and the required Kenyan compliance process must be verified before production use.

## Objectives

- Integrate with Zoho Books using its supported APIs and authentication mechanisms.
- Run the device integration on the Windows machine connected to the fiscal control unit.
- Use the supplied Tremol SDK and Kenya CU protocol documentation as the implementation references.
- Keep invoice processing, device communication, and external API concerns separated.
- Prevent duplicate fiscalization when a request times out or the device response is uncertain.
- Maintain auditable processing records, structured logs, automated tests, and reliable recovery procedures.
- Build and test changes through GitHub Actions before merging them into protected branches.

## Architecture

The solution separates the cloud integration from the local hardware integration.

- **API:** application endpoints and integration entry points.
- **Domain:** core business concepts and rules.
- **Application:** invoice processing use cases and orchestration.
- **Infrastructure:** external integrations, persistence, and technical implementations.
- **DeviceAgent:** the Windows-hosted worker responsible for local device operations.
- **Tests:** unit, integration, and contract tests.

The exact responsibilities may be refined as the implementation develops and the SDK's actual requirements are verified.

### Intended invoice workflow

1. An invoice is created in Zoho Books.
2. ZohoAmarco retrieves and validates the invoice data.
3. The application records a durable processing operation before attempting fiscalization.
4. The local device integration submits the appropriate operation through the verified Tremol SDK/protocol.
5. The application checks the device response and captures the applicable fiscal details.
6. The result is associated with the original Zoho Books invoice.
7. If the result is uncertain, the operation is reconciled before another fiscalization attempt is considered.

The application must not treat a network timeout as proof that fiscalization failed. The device may have completed the operation even when the response was lost.

## Technical references

Implementation must be guided by these supplied documents:

- **`Manual.pdf`** — Tremol fiscal device libraries and file server documentation, including the C# library, `ZFPLabServer`, connection setup, command execution, and exception handling.
- **`protocol_description_KE_CU_2509041522.pdf`** — Kenyan control unit protocol, including message framing, responses, status/error codes, and receipt-related commands.

The manuals must be checked against the actual G03 device, its firmware, and the SDK package version used by the project. Do not invent command names, parameters, or response fields.

## Technology

- C# and .NET
- JetBrains Rider
- Git and GitHub
- GitHub Actions for continuous integration
- Zoho Books API
- Tremol SDK and Kenyan CU protocol

Specific .NET versions, persistence technology, deployment settings, and SDK dependencies must match the verified project configuration.

## Repository structure

```text
ZohoAmarco/
├── .github/
│   └── workflows/
├── src/
│   ├── ZohoAmarco.Api/
│   ├── ZohoAmarco.Domain/
│   ├── ZohoAmarco.Application/
│   ├── ZohoAmarco.Infrastructure/
│   └── ZohoAmarco.DeviceAgent/
├── tests/
│   ├── ZohoAmarco.UnitTests/
│   ├── ZohoAmarco.IntegrationTests/
│   └── ZohoAmarco.ContractTests/
├── ZohoAmarco.sln
└── README.md
```

The actual repository structure is authoritative; update this section whenever the solution structure changes.

## Git branching and contribution workflow

The project follows this promotion path:

```text
main
  ↑
 dev
  ↑
feature/*
```

### Branch responsibilities

| Branch | Purpose |
|---|---|
| `main` | Stable, production-ready code and release history |
| `dev` | Integration branch for completed and tested features |
| `feature/*` | Isolated development work for a specific task |

### Creating a feature branch

Always start new feature work from the latest `dev` branch:

```bash
git switch dev
git pull origin dev
git switch -c feature/your-feature-name
```

Use descriptive names such as `feature/zoho-books-client`, `feature/tremol-device-adapter`, or `feature/invoice-fiscalization`.

### Committing and publishing work

```bash
git add .
git diff --cached
git commit -m "feat: describe the completed change"
git push -u origin feature/your-feature-name
```

Review the staged changes before committing. Never commit API secrets, credentials, device passwords, tokens, or production configuration containing secrets.

### Pull request and merge process

1. Push the feature branch to GitHub.
2. Open a pull request from `feature/*` into `dev`.
3. Wait for CI checks and required reviews.
4. Merge the feature into `dev` and test the integrated changes.
5. When the release is ready, open a pull request from `dev` into `main`.
6. Merge only after CI passes and the release is approved.

Do not develop features directly on `main`. Keep `main` protected and require pull requests and successful CI checks where repository settings permit.

## Development commands

Run commands from the repository root.

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

Use the .NET SDK version pinned or specified by the repository. Resolve formatting, build, and test failures before opening a pull request.

## Engineering and security requirements

- Use configuration and a secure secret store for credentials.
- Use least-privilege Zoho OAuth scopes.
- Do not expose the local fiscal device service directly to the public internet.
- Validate invoice fields, tax mappings, totals, payment information, and supported receipt types.
- Serialize device commands when required by the SDK and device protocol.
- Persist operation state and device responses for audit and reconciliation.
- Implement bounded retries only for operations known to be safe to retry.
- Never automatically issue a second fiscal receipt merely because the first response timed out.
- Redact secrets and sensitive information from logs.
- Test against a suitable development or test device before production use.
- Verify all applicable Kenyan fiscalization and certification requirements before claiming compliance.

## CI/CD

GitHub Actions should validate pull requests and changes merged into the relevant branches by restoring dependencies, checking formatting, building the solution, and running automated tests.

Release workflows may publish deployable artifacts after the required checks pass. Production deployment and device configuration must be handled as explicit, controlled steps rather than assumed to happen when code is pushed.

## Current development priorities

1. Establish the solution structure and Git workflow.
2. Verify the SDK package, device compatibility, and connection method.
3. Implement and test a minimal device status/read operation.
4. Build the Zoho Books integration and invoice mapping.
5. Implement safe fiscalization orchestration and durable operation tracking.
6. Add reconciliation, observability, and deployment automation.
7. Validate the complete workflow in an appropriate test environment before production use.
