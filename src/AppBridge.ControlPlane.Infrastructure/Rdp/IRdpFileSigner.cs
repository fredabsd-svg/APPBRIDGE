namespace AppBridge.ControlPlane.Infrastructure.Rdp;

/// <summary>
/// ADR-0009 item 1: "uma única responsabilidade — receber o conteúdo do <c>.rdp</c> e devolver o
/// conteúdo assinado." Nenhum outro componente do Control Plane sabe como a assinatura acontece.
///
/// A implementação de MVP-0 (<see cref="RdpSignExeSigner"/>) depende de <c>rdpsign.exe</c>, um
/// executável do Windows — este ambiente de desenvolvimento (sandbox Linux) não o tem, então essa
/// implementação não pode ser verificada de ponta a ponta aqui (T-502, S010). O que os testes desta
/// tarefa verificam é a orquestração do processo (argumentos, timeout, leitura do arquivo,
/// limpeza) contra um executável de teste controlado, não a validade real de uma assinatura RDP.
/// </summary>
public interface IRdpFileSigner
{
    /// <summary>
    /// Nunca devolve um <c>.rdp</c> sem assinatura (RNF-002, ADR-0009 item 2) — falha vira
    /// <see cref="RdpSigningFailedException"/>, não um resultado degradado.
    /// </summary>
    Task<string> SignAsync(string unsignedRdpContent, CancellationToken cancellationToken = default);
}

/// <summary>Assinatura de <see cref="RdpSignExeSigner"/> — o processo de assinatura falhou; o chamador trata isso como falha de lançamento (RNF-002), nunca como .rdp utilizável.</summary>
public sealed class RdpSigningFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);
