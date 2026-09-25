# APPBRIDGE

Plataforma de distribuição de aplicativos Windows remotos sobre RDS/RemoteApp. O repositório contém
o Control Plane e um launcher de console para validar o caminho mínimo do MVP-0a; o cliente WinUI 3,
MSIX, agent e painel admin pertencem às fases seguintes.

## Desenvolvimento

Consulte [Desenvolvimento e operação local](docs/operacao/desenvolvimento-control-plane.md) para
configurar PostgreSQL, Entra ID, certificados RDP, seed do catálogo, Control Plane e launcher. O SDK
necessário é .NET 10. O perfil local do Control Plane escuta apenas em `http://127.0.0.1:5080`.
