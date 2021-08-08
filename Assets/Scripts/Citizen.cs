using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class Citizen : MonoBehaviour
{
	public enum BusinessStrategy
	{
		NONE,
		GATHERING,
		BUYING,
		SELLING,
		SERVICING
	}

	public bool IsFrozen { get; set; }

	public float BaseResBoxValuation => _baseResBoxValuation;

	public float MoneyInWallet => _moneyInWallet;

	public float AverageResBoxAcquisitionCost => _totalGatheringTravelCosts / _totalResBoxesGathered;

	public int TotalResBoxesOwned => _totalResBoxesOwned;

	/// <summary>
	/// Yields no profit, never goes below the citizen's base valuation of one res box.
	/// </summary>
	public float MinUnitSellingPrice => AverageResBoxAcquisitionCost + Mathf.Max(_baseResBoxValuation, _baseResBoxValuation / (_totalResBoxesOwned + 1) * _gameManager.PriceMagnifier);

	public float MaxUnitBuyingPrice => MinUnitSellingPrice * (1 - _minimumProfitExpectation); // wants at the very least the profit potential he currently expects

	public float CurrentBuyingPriceGoal => MinUnitSellingPrice * (1 - _currentProfitExpectation);

	public float CurrentSellingPriceGoal => MinUnitSellingPrice * (1 + _currentProfitExpectation);

	public float ProfitGrowth => _profitGrowth;

	public bool AreProfitsIncreasing => ProfitGrowth > 0;

	public float TotalProfits => MoneyInWallet - _startingMoney;

	public float ProfitPerSecond => TotalProfits / _totalSeconds;

	public BusinessStrategy CurrentStrategy => _currentStrategy;

	public event Action DestinationReached;

	private GameManager _gameManager;
	private TradeManager _tradeManager;
	private Vector3 _origin;
	private Vector3 _target;
	private GameObject _targetObject;
	private Vector3 _anchor;
	private float _timer;
	private float _totalSeconds;
	private float _restrategizingMoment;

	[Header("Boring Stuff")]
	[SerializeField] private TMP_Text _moneyLabel;
	[SerializeField] private TMP_Text _secondLabel;
	[SerializeField] private TMP_Text _thirdLabel;

	[SerializeField] private float _speed = 5f;
	[SerializeField] private float _fieldRadius = 20f;
	[SerializeField] private float _idlingRadius = .5f;

	[Header("Interesting Stuff")]
	[Tooltip("Starts out random between 0 and 1, but can fluctuate by effect of profit margins.")]
	[SerializeField]
	private float _baseResBoxValuation;

	[Tooltip("The primary asset that can be found or traded as merchandise.")]
	[SerializeField]
	private int _totalResBoxesOwned;

	[Tooltip("The amount of seconds to wait before deciding on the next action.")]
	[SerializeField]
	private float _opportunityWaitingTime = 1f;

	//[Tooltip("The amount of seconds to wait before trying to trade again with the last trade partner that failed to strike a deal.")]
	//[SerializeField] float _badTradePartnerBanTime = 10f;

	[Tooltip("The citizen won't accept a profit margin lower than this decimal fraction.")]
	[SerializeField]
	private float _minimumProfitExpectation = 0.01f;

	[Tooltip("The profit margin (in decimal fraction) the citizen will open with at its first trading negotiation.")]
	[SerializeField]
	private float _startingProfitExpectation = 0.1f;

	[Tooltip("The decimal fraction of this citizen's total worth that he's willing to invest in trading.")]
	[SerializeField]
	private float _tradeInvestmentFraction = 0.2f;

	[Header("Read-Only")]
	[SerializeField, ReadOnly] private float _moneyInWallet;
	[SerializeField, ReadOnly] private float _startingMoney;
	[SerializeField, ReadOnly] private int _startingResBoxes;
	[SerializeField, ReadOnly] private float _maxBuyingPriceMetric;
	[SerializeField, ReadOnly] private float _minSellingPriceMetric;
	//[SerializeField, ReadOnly] private bool _isDoomed; // meaning they dont have an existing cashflow + dont own assets + cant afford any type of investment right now
	[SerializeField, ReadOnly] private bool _isIdling; // just for aesthetic effect, doesn't cost money
	[SerializeField, ReadOnly] private BusinessStrategy _currentStrategy;
	//[SerializeField, ReadOnly] private bool _wantsToTrade;
	[SerializeField, ReadOnly] private int _totalResBoxesGathered;
	[SerializeField, ReadOnly] private float _totalGatheringTravelCosts;
	[SerializeField, ReadOnly] private float _latestProfit;
	[SerializeField, ReadOnly] private float _latestProfitMargin;
	[SerializeField, ReadOnly] private float _profitGrowth;
	[SerializeField, ReadOnly] private float _currentProfitExpectation;
	[SerializeField, ReadOnly] private float _currentBuyingGoalMetric;
	[SerializeField, ReadOnly] private float _currentSellingGoalMetric;
	[SerializeField, ReadOnly] private Citizen _lastFailedTradePartner;

	public void AddMoney(float money)
	{
		_moneyInWallet += money;
	}

	public void AddResBoxes(int amount)
	{
		_totalResBoxesOwned += amount;
	}

	public void GiveStartingCapital(float cashAmount, int resBoxAmount)
	{
		AddResBoxes(resBoxAmount);
		AddMoney(cashAmount);
		_startingMoney = cashAmount;
		_startingResBoxes = resBoxAmount;
	}

	public bool CanPay(float price)
	{
		return price <= MoneyInWallet;
	}

	public bool TryAskForAttention()
	{
		IsFrozen = true;
		_currentStrategy = BusinessStrategy.NONE;
		return true; // always succeeds for now
	}

	public void ReleaseAttention()
	{
		IsFrozen = false;
		SetIdling(true);
	}

	public void Halt(float timeUntilRestrategizing = 0f)
	{
		_anchor = transform.position;
		_target = _anchor;
		_targetObject = null;
		DestinationReached = null;
		_restrategizingMoment = _totalSeconds + timeUntilRestrategizing;
	}

	void Awake()
	{
		_gameManager = FindObjectOfType<GameManager>();
		_tradeManager = FindObjectOfType<TradeManager>();

		_timer = 1;

		_origin = transform.position;
		_target = _origin;
		_anchor = _origin;

		_baseResBoxValuation = Random.value;
		_currentProfitExpectation = _startingProfitExpectation;
		_maxBuyingPriceMetric = MaxUnitBuyingPrice;
		_minSellingPriceMetric = MinUnitSellingPrice;
		_currentBuyingGoalMetric = CurrentBuyingPriceGoal;
		_currentSellingGoalMetric = CurrentSellingPriceGoal;
	}

	void Start()
	{
		SetIdling(true);
	}

	void Update()
	{
		Tick();
		RefreshMoneyLabel();

		if (IsFrozen)
			return;

		bool shouldHustle = _isIdling && !AreProfitsIncreasing && _restrategizingMoment < _totalSeconds;
		if (shouldHustle)
		{
			BusinessStrategy bestStrategy = CalculateBestStrategy();
			bool succesfullyStarted = TryStartStrategy(bestStrategy);

			if (!succesfullyStarted) // If we're stumped, idle for a bit longer until new opportunity arises
			{
				SetIdling(true);
				_restrategizingMoment = _totalSeconds + _opportunityWaitingTime;
			}
		}

		// Idling (no cost)
		if (_isIdling && Random.value < .001f)
		{
			_target.x = Mathf.Clamp(_anchor.x + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
			_target.z = Mathf.Clamp(_anchor.z + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
		}

		// Traveling
		float targetDist = Vector3.Distance(transform.position, _target);
		bool isBusinessTrip = !_isIdling;

		bool destinationReached = targetDist <= .001f;
		if (destinationReached)
		{
			if (isBusinessTrip)
			{
				_anchor = transform.position;

				if (_currentStrategy == BusinessStrategy.GATHERING)
				{
					SetIdling(true);
				}

				DestinationReached?.Invoke();
			}
		}
		else
		{
			bool resBoxDisappeared = isBusinessTrip && _currentStrategy == BusinessStrategy.GATHERING && _targetObject == null;
			if (resBoxDisappeared)
			{
				SetIdling(true);
			}

			Move();
		}
	}

	void Move()
	{
		Vector3 nextStep = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);

		bool isBusinessTrip = !_isIdling;
		if (isBusinessTrip)
		{
			float stepDist = Vector3.Distance(transform.position, nextStep);
			float travelCosts = stepDist * _gameManager.TravelCostPerMeter;
			bool canAffordStep = travelCosts <= MoneyInWallet;

			if (!canAffordStep)
				return;

			AddMoney(-travelCosts);

			if (_currentStrategy == BusinessStrategy.GATHERING)
			{
				_totalGatheringTravelCosts += travelCosts;
			}
		}

		nextStep.y = _origin.y;
		transform.position = nextStep;
	}

	void SetIdling(bool idle)
	{
		if (idle)
		{
			Halt();

			_isIdling = true;
			_currentStrategy = BusinessStrategy.NONE;

			RefreshSecondaryLabels();
		}
		else
		{
			_isIdling = false;
		}
	}

	bool TryStartStrategy(BusinessStrategy strategy)
	{
		SetIdling(false);
		_anchor = transform.position;
		_currentStrategy = strategy;

		switch (strategy)
		{
			case BusinessStrategy.NONE:
				return false;
			case BusinessStrategy.GATHERING:
				return TryGatherResBox();
			case BusinessStrategy.BUYING:
				return TryBuyingFromNearbyCitizen();
			case BusinessStrategy.SELLING:
				return TrySellingToNearbyCitizen();
 			case BusinessStrategy.SERVICING:
				return false;
			default:
				return false;
		}
	}

	BusinessStrategy CalculateBestStrategy()
	{
		BusinessStrategy strategy = BusinessStrategy.NONE;
		float lowestCost = float.PositiveInfinity;

		// Gathering
		float gatheringCost = CalculateGatheringCost();
		if (gatheringCost <= MoneyInWallet && gatheringCost < lowestCost)
		{
			lowestCost = gatheringCost;
			strategy = BusinessStrategy.GATHERING;
		}

		// Buying
		if (CurrentBuyingPriceGoal <= MoneyInWallet)
		{
			float buyingCost = CalculateBuyingCost();
			if (buyingCost <= MoneyInWallet && buyingCost < lowestCost)
			{
				lowestCost = buyingCost;
				strategy = BusinessStrategy.BUYING;
			}
			else
			{
				_tradeInvestmentFraction = Mathf.Clamp(_tradeInvestmentFraction * 0.9f, 0f, 1f);
			}
		}

		// Selling
		if (_totalResBoxesOwned > 0)
		{
			float sellingCost = CalculateSellingCost();
			if (sellingCost <= MoneyInWallet && sellingCost < lowestCost)
			{
				lowestCost = sellingCost;
				strategy = BusinessStrategy.SELLING;
			}
			else
			{
				_tradeInvestmentFraction = Mathf.Clamp(_tradeInvestmentFraction * 1.1f, 0f, 1f);
			}
		}

		return strategy;
	}

	#region Gathering Strategy
	bool TryGatherResBox()
	{
		var closestBox = GetClosestResBox();
		if (closestBox == null)
			return false;

		_targetObject = closestBox.gameObject;
		_target = closestBox.transform.position;
		_target.y = _origin.y;

		return true;
	}

	float CalculateGatheringCost()
	{
		var closestBox = GetClosestResBox();
		if (closestBox == null)
			return float.PositiveInfinity; // maybe not the most future-proof output, but it should work for now

		float dist = Vector3.Distance(_anchor, closestBox.transform.position);

		return dist * _gameManager.TravelCostPerMeter;
	}

	ResBox GetClosestResBox()
	{
		var boxes = FindObjectsOfType<ResBox>();
		if (boxes.Length <= 0)
		{
			return null;
		}

		ResBox closestBox = boxes[0];
		float closestDist = Vector3.Distance(_anchor, boxes[0].transform.position);
		foreach (var box in boxes)
		{
			float dist = Vector3.Distance(_anchor, box.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
				closestBox = box;
			}
		}

		return closestBox;
	}
	#endregion

	#region Trading Strategy
	bool TryBuyingFromNearbyCitizen()
	{
		_currentBuyingGoalMetric = CurrentBuyingPriceGoal;

		if (CurrentBuyingPriceGoal > MoneyInWallet)
			return false;

		if (IsFrozen) // shouldnt happen but just in case
			return false;

		if (!TryFindTargetCitizen())
			return false;

		Citizen other = _targetObject.GetComponent<Citizen>();
		if (other.TryAskForAttention())
		{
			DestinationReached += AskToBuyFromTarget;
			return true;
		}

		return false;
	}

	void AskToBuyFromTarget()
	{
		if (_currentStrategy != BusinessStrategy.BUYING)
		{
			DestinationReached -= AskToBuyFromTarget;
			return;
		}

		Citizen other = _targetObject.GetComponent<Citizen>();
		if (other == null)
			return;

		bool isOtherBusy = other.CurrentStrategy != BusinessStrategy.NONE && other.CurrentStrategy != BusinessStrategy.SELLING;
		if (isOtherBusy)
			return;

		other.ReleaseAttention();
		other.Halt(3f);

		Halt(3f);

		float unitOffer = CurrentBuyingPriceGoal;
		int buyAmount = Mathf.CeilToInt(MoneyInWallet * _tradeInvestmentFraction / unitOffer);

		Debug.Log($"<color=lime><b>{name}</b> wants to <b>buy {buyAmount} {GameManager.ResBoxSymbol}</b> from <b>{other.name}</b> for <b>{GameManager.FormatMoney(unitOffer * buyAmount)}</b>...</color>");

		bool success = _tradeManager.TryMakeBoxTrade(this, other, buyAmount, unitOffer);

		if (success)
		{
			_currentProfitExpectation *= 1.1f; // Business is good, increase profit expectations
		}
		else
		{
			_currentProfitExpectation *= 0.9f; // Business sucks, decrease profit expectations
			_lastFailedTradePartner = other;
		}

		SetIdling(true);

		DestinationReached -= AskToBuyFromTarget;
	}

	bool TrySellingToNearbyCitizen()
	{
		_currentSellingGoalMetric = CurrentSellingPriceGoal;

		if (_totalResBoxesOwned <= 0)
			return false;

		if (IsFrozen)
			return false;

		if (TryFindTargetCitizen())
		{
			Citizen other = _targetObject.GetComponent<Citizen>();
			if (other.TryAskForAttention())
			{
				DestinationReached += AskToSellToTarget;
				return true;
			}
		}

		return false;
	}

	void AskToSellToTarget()
	{
		if (_currentStrategy != BusinessStrategy.SELLING)
		{
			DestinationReached -= AskToSellToTarget;
			return;
		}

		Citizen other = _targetObject.GetComponent<Citizen>();
		if (other == null)
			return;

		bool isOtherBusy = other.CurrentStrategy != BusinessStrategy.NONE && other.CurrentStrategy != BusinessStrategy.BUYING;
		if (isOtherBusy)
			return;

		other.ReleaseAttention();
		other.Halt(3f);

		Halt(3f);

		float unitOffer = CurrentSellingPriceGoal;
		int sellAmount = Mathf.CeilToInt(_totalResBoxesOwned * (1 - _tradeInvestmentFraction));

		Debug.Log($"<color=red><b>{name}</b> wants to <b>sell {sellAmount} {GameManager.ResBoxSymbol}</b> to <b>{other.name}</b> for <b>{GameManager.FormatMoney(unitOffer * sellAmount)}</b>...</color>");

		bool success = _tradeManager.TryMakeBoxTrade(other, this, sellAmount, unitOffer);

		if (success)
		{
			_currentProfitExpectation *= 1.1f; // Business is good, increase profit expectations
		}
		else
		{
			_currentProfitExpectation *= 0.9f; // Business sucks, decrease profit expectations
			_lastFailedTradePartner = other;
		}

		SetIdling(true);

		DestinationReached -= AskToSellToTarget;
	}

	float CalculateBuyingCost()
	{
		float citizenFindingCost = CalculateCitizenFindingCost();
		int buyAmount = Mathf.CeilToInt(MoneyInWallet * _tradeInvestmentFraction / CurrentBuyingPriceGoal);
		float buyingCost = CurrentBuyingPriceGoal * buyAmount;
		float incentive = TotalProfits / ProfitPerSecond;
		float totalCost = citizenFindingCost + buyingCost - incentive;

		return Mathf.Max(citizenFindingCost, totalCost);
	}

	float CalculateSellingCost()
	{
		float citizenFindingCost = CalculateCitizenFindingCost();
		int sellAmount = Mathf.CeilToInt(_totalResBoxesOwned * _tradeInvestmentFraction);
		float sellingCost = MinUnitSellingPrice * sellAmount;
		float incentive = TotalProfits / ProfitPerSecond;
		float totalCost = citizenFindingCost + sellingCost - incentive;

		return Mathf.Max(citizenFindingCost, totalCost);
	}

	float CalculateCitizenFindingCost()
	{
		var closestCitizen = GetClosestCitizen();
		if (closestCitizen == null)
			return float.PositiveInfinity; // maybe not the most future-proof output, but it should work for now

		float dist = Vector3.Distance(_anchor, closestCitizen.transform.position);

		return dist * _gameManager.TravelCostPerMeter;
	}
	#endregion

	bool TryFindTargetCitizen()
	{
		var closestCitizen = GetClosestCitizen();
		if (closestCitizen == null || closestCitizen == _lastFailedTradePartner)
			return false;

		_targetObject = closestCitizen.gameObject;
		_target = closestCitizen.transform.position;
		_target.y = _origin.y;

		// Small offset so not to run into eachother
		Vector3 dir = (_target - transform.position).normalized;
		_target -= dir * 2;

		return true;
	}

	Citizen GetClosestCitizen()
	{
		var citizens = FindObjectsOfType<Citizen>();
		if (citizens.Length <= 0)
		{
			return null;
		}

		Citizen closestCitizen = (citizens[0] == this || citizens[0] == _lastFailedTradePartner) ? citizens[1] : citizens[0];
		float closestDist = Vector3.Distance(_anchor, citizens[0].transform.position);
		foreach (var citizen in citizens)
		{
			if (citizen == this || citizen == _lastFailedTradePartner)
				continue;

			float dist = Vector3.Distance(_anchor, citizen.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
				closestCitizen = citizen;
			}
		}

		return closestCitizen;
	}

	#region Bookkeeping
	public void RegisterSale(float revenue, int resBoxesSold)
	{
		float profit = revenue - MinUnitSellingPrice * resBoxesSold;
		RegisterProfit(profit);

		float profitMargin = profit / revenue;
		_latestProfitMargin = profitMargin;

		_maxBuyingPriceMetric = MaxUnitBuyingPrice;
		_minSellingPriceMetric = MinUnitSellingPrice;
	}

	void RegisterProfit(float profit)
	{
		_profitGrowth = _latestProfit == 0 ? 1 : (profit / _latestProfit - 1);
		_latestProfit = profit;

		// Good business makes us care slightly more about unit value, and vice-versa
		_baseResBoxValuation = Mathf.Max(0f, _baseResBoxValuation * (1 + (_profitGrowth / 10)));

		RefreshSecondaryLabels();
	}
	#endregion

	void Tick()
	{
		_timer -= Time.deltaTime;

		if (_timer <= 0)
		{
			_timer = 1;
			_totalSeconds++;
		}
	}

	void RefreshMoneyLabel()
	{
		_moneyLabel.text = $"{GameManager.FormatMoney(MoneyInWallet)}";
	}

	void RefreshSecondaryLabels()
	{
		string boxStatus = $"{_totalResBoxesOwned} {GameManager.ResBoxSymbol}";

		_secondLabel.text = boxStatus;
		_secondLabel.color = new Color(0f, 0.75f, 1f);

		float profitValue = _latestProfitMargin;
		string profitStatus = profitValue == 0 ? "-" : $"{(profitValue > 0 ? "+" : "")}{profitValue * 100:n2}%";

		_thirdLabel.text = profitStatus;
		_thirdLabel.color = profitValue < 0 ? Color.red : Color.green;
	}

	void OnTriggerEnter(Collider collision)
	{
		var box = collision.gameObject.GetComponent<ResBox>();
		if (box != null && !_isIdling)
		{
			AddResBoxes(1);

			Destroy(collision.gameObject);

			_secondLabel.text = $"+1 {GameManager.ResBoxSymbol}";
			_secondLabel.color = Color.cyan;

			// Metrics
			_maxBuyingPriceMetric = MaxUnitBuyingPrice;
			_minSellingPriceMetric = MinUnitSellingPrice;
			_totalResBoxesGathered++;
		}
	}
}
