namespace DafnyContractVerification; 

public class Contradiction : ContractChecker {
  //Take in set of contracts
  //Return a set of contracts
  //Modify or create new set and add to it individually
  public Contradiction() {}

  public override List<string> evaluate(List<AttributedExpression> contracts) {
    throw new NotImplementedException();
  }
}