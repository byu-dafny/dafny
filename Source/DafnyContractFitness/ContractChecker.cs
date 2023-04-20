using System.Linq.Expressions;

namespace DafnyContractVerification; 

public abstract class ContractChecker {
  public ContractChecker() {}

  public abstract List<String> evaluate(List<BinaryExpression> requires, List<BinaryExpression> ensures);
}