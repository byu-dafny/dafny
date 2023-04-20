using System.Linq.Expressions;

namespace DafnyContractVerification; 

public class Contradiction : ContractChecker {
  //Take in set of contracts
  //Return a set of contracts
  //Modify or create new set and add to it individually
  public Contradiction() {}

  public override List<string> evaluate(List<BinaryExpression> requires, List<BinaryExpression> ensures) {
    //&& all requires together and all ensures together 
    string completeReq = "";
    foreach (BinaryExpression contract in requires) {
      //if its a requires do something
      completeReq += " && " + contract.ToString();
    }
    
    //
    throw new NotImplementedException();
  }
}