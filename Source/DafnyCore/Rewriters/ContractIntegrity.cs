namespace Microsoft.Dafny; 

public class ContractIntegrity : IRewriter {
  internal ContractIntegrity(ErrorReporter reporter) : base(reporter)
  {
  }
}