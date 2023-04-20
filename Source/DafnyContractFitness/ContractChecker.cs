namespace DafnyContractVerification; 

public abstract class ContractChecker {
  public ContractChecker() {}

  public abstract List<String> evaluate(List<AttributedExpression> contracts);
}