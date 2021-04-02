using TMPro;
using UnityEngine;

public class Citizen : MonoBehaviour
{
	GameManager gameManager;
	Vector3 origin;
	Vector3 target;
	Vector3 anchor;
	float timer;
	float totalSeconds;
	float retryMoment;

	public TMP_Text moneyText;
	public TMP_Text secondaryText;
	public float fieldRadius = 20f;
	public float speed = 5f;
	public float baseResBoxValuation = 0f; // starts out random, but can fluctuate by effect of profit margins
	public int totalResBoxesOwned; // the primary asset that can be found or bought
	[SerializeField]
	float money;
	public float idlingRadius = 2f;

	[Header("Read-Only")]
	public bool isDoomed; // meaning they dont have an existing cashflow + dont own assets + cant afford any type of investment right now
	[SerializeField]
	bool isIdling; // just for aesthetic effect, doesn't cost money

	[SerializeField]
	float lastSaleProfit;

	[SerializeField]
	float profitTrend;

	// Never goes below the citizen's base valuation of one res box
	public float MinimumResBoxSellingPrice => Mathf.Max(baseResBoxValuation, baseResBoxValuation / (totalResBoxesOwned + 1) * gameManager.priceMagnifier);

	public float ProfitTrend => profitTrend;

	public bool AreProfitsIncreasing => ProfitTrend > 0;

	public float TotalProfits => money - gameManager.citizenStartingCapital;

	public float ProfitPerSecond => TotalProfits / totalSeconds;

	void Start()
	{
		gameManager = FindObjectOfType<GameManager>();

		timer = 1;

		origin = transform.position;
		target = origin;

		baseResBoxValuation = Random.value;

		GetComponent<Renderer>().material.color = Random.ColorHSV(0, 1, 1, 1, 1, 1, 1, 1);

		StartIdling();
	}

	void Update()
	{
		Tick();
		RefreshHUD();

		bool shouldHustle = isIdling && !AreProfitsIncreasing && retryMoment < totalSeconds;
		if (shouldHustle)
		{
			bool canHustle = TryStartBusiness();
			if (canHustle)
			{
				isIdling = false;
			}
			else // if we cant hustle, idle for a bit longer until new opportunity arises
			{
				retryMoment = totalSeconds + 2;
			}
		}

		// Idling (no cost)
		if (isIdling && Random.value < .001f)
		{
			target.x = Mathf.Clamp(anchor.x + Random.Range(-1f, 1f) * idlingRadius, -fieldRadius, fieldRadius);
			target.z = Mathf.Clamp(anchor.z + Random.Range(-1f, 1f) * idlingRadius, -fieldRadius, fieldRadius);
		}

		// Traveling
		float targetDist = Vector3.Distance(transform.position, target);
		bool isBusinessTrip = !isIdling;

		bool destinationReached = targetDist <= .001f;
		if (destinationReached)
		{
			if (isBusinessTrip)
			{
				StartIdling();
			}
		}
		else
		{
			Vector3 nextStep = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

			if (isBusinessTrip)
			{
				float stepDist = Vector3.Distance(transform.position, nextStep);
				float travelCosts = stepDist * gameManager.transportCostPerMeter;
				bool canAffordStep = travelCosts <= money;

				if (!canAffordStep)
					return;

				AddMoney(-travelCosts);
			}

			nextStep.y = origin.y;
			transform.position = nextStep;
		}
	}

	public void AddMoney(float money)
	{
		this.money += money;

		//secondaryText.text = $"{(money > 0 ? "+" : "")}${money:n2}";
		//secondaryText.color = Color.magenta;
	}

	void StartIdling()
	{
		anchor = transform.position;
		isIdling = true;

		secondaryText.text = $"{totalResBoxesOwned} boxes";
		secondaryText.color = new Color(0f, 0.5f, 1f);
	}

	void RegisterProfit(float profit)
	{
		profitTrend = profit / (lastSaleProfit == 0 ? profit : lastSaleProfit) - 1;
		lastSaleProfit = profit;

		// Display the profit
		secondaryText.text = ProfitTrend == 0 ? "" : $"{(AreProfitsIncreasing ? "+" : "") + (ProfitTrend * 100):n2}%";
		secondaryText.color = AreProfitsIncreasing ? Color.green : Color.red;
	}

	bool TryStartBusiness()
	{
		bool foundStrategy = false;

		// Find the current most profitable strategy
		if (GetGatheringCost() <= money)
		{
			foundStrategy = TryGatherResBox();
		}

		if (foundStrategy)
		{
			anchor = transform.position;
		}

		return foundStrategy;
	}

	#region Gathering Strategy
	bool TryGatherResBox()
	{
		var closestBox = GetClosestResBox();
		if (closestBox == null)
			return false;

		target = closestBox.transform.position;
		target.y = origin.y;

		return true;
	}

	float GetGatheringCost()
	{
		var closestBox = GetClosestResBox();
		if (closestBox == null)
			return float.PositiveInfinity; // maybe not the most future-proof output, but it should work for now

		float dist = Vector3.Distance(transform.position, closestBox.transform.position);

		return dist * gameManager.transportCostPerMeter;
	}

	ResBox GetClosestResBox()
	{
		var boxes = FindObjectsOfType<ResBox>();
		if (boxes.Length <= 0)
		{
			return null;
		}

		ResBox closestBox = boxes[0];
		float closestDist = Vector3.Distance(transform.position, boxes[0].transform.position);
		foreach (var box in boxes)
		{
			float dist = Vector3.Distance(transform.position, box.transform.position);
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
	public bool TryBuyResBox(int amount, float offer, Citizen customer)
	{
		// Considerations
		if (totalResBoxesOwned < amount) // can i sell that much?
			return false;

		float price = MinimumResBoxSellingPrice + ((offer - MinimumResBoxSellingPrice) / 2); // just compromise in the middle, keep it simple for now

		if (price < MinimumResBoxSellingPrice) // would i lose money?
			return false;

		// Perform the sale
		SellResBox(amount, price, customer);

		return true;
	}

	void SellResBox(int amount, float price, Citizen customer)
	{
		// Transaction
		totalResBoxesOwned -= amount;
		AddMoney(price);
		customer.AddMoney(-price);
		customer.totalResBoxesOwned += amount;

		// Book keeping
		float profit = price - MinimumResBoxSellingPrice;
		RegisterProfit(profit);
	}
	#endregion

	void Tick()
	{
		timer -= Time.deltaTime;

		if (timer <= 0)
		{
			timer = 1;
			totalSeconds++;
		}
	}

	void RefreshHUD()
	{
		moneyText.text = $"ƒ{money:n2}";
	}

	void OnTriggerEnter(Collider collision)
	{
		var box = collision.gameObject.GetComponent<ResBox>();
		if (box)
		{
			totalResBoxesOwned++;
			Destroy(collision.gameObject);

			secondaryText.text = "+1 box";
			secondaryText.color = Color.cyan;
		}
	}
}
