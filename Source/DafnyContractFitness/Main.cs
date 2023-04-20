using System.Collections.Generic;

namespace DafnyContractVerification;

public class TestClass 
{
  static void Main(string[] args) 
  {
    Contradiction contradictionChecker = new Contradiction();
    Redundant redundancyChecker = new Redundant();
    Vacuous vacuityChecker = new Vacuous();
    Unconstrained unconstrainedChecker = new Unconstrained();

    List<ContractChecker> checkerList = new List<ContractChecker>();
    checkerList.Add(contradictionChecker);
    checkerList.Add(redundancyChecker);
    checkerList.Add(vacuityChecker);
    checkerList.Add(unconstrainedChecker);
    //Standin for actual contracts
    List<AttributedExpression> contracts = new List<AttributedExpression>();
    foreach (ContractChecker checker in checkerList) {
      checker.evaluate(contracts);
    }
  }
}