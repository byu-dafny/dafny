
using System.Linq.Expressions;

namespace DafnyContractVerification;

public class Redundant : ContractChecker {
    //Take in set of contracts
    //Return feedback on the redundancy of the contracts 
    //Modify or create new set and add to it individually
    public Redundant() {}
    public override List<string> evaluate(List<BinaryExpression> requires, List<BinaryExpression> ensures) {
      throw new NotImplementedException();
    }
  }