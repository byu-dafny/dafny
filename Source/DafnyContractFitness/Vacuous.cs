using System.Linq.Expressions;

namespace DafnyContractVerification; 

public class Vacuous : ContractChecker {
  //Take in set of contracts
  //Return a set of contracts
  //Modify or create new set and add to it individually
  public Vacuous() {}
  public override List<string> evaluate(List<BinaryExpression> requires, List<BinaryExpression> ensures) {
    throw new NotImplementedException();
  }
}