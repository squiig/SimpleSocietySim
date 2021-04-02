using TMPro;
using UnityEngine;

public class Citizen : MonoBehaviour
{
	GameManager gameManager;
	Vector3 origin;
	[SerializeField]
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
			target.x = Mathf.Clamp(anchor.x + Random.Range(-1, 1) * idlingRadius, -fieldRadius, fieldRadius);
			target.z = Mathf.Clamp(anchor.z + Random.Range(-1, 1) * idlingRadius, -fieldRadius, fieldRadius);
		}

		float targetDist = Vector3.Distance(transform.position, target);
		bool canAffordTransport = targetDist * gameManager.transportCostPerMeter <= money;

		if (!canAffordTransport && !isIdling)
			return;

		bool destinationReached = targetDist <= .01f;
		if (destinationReached)
		{
			if (!isIdling) // were we on a business trip?
			{
				StartIdling();
			}
		}
		else
		{
			// Walk
			Vector3 nextStep = Vector3.MoveTowards(anchor, target, speed * Time.deltaTime);
			nextStep.y = origin.y;
			transform.position = nextStep;

			if (!isIdling)
			{
				float stepDist = Vector3.Distance(transform.position, nextStep);
				AddMoney(-stepDist * gameManager.transportCostPerMeter);
			}
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
		bool success = false;

		// Find the current most profitable strategy
		if (GetGatheringCost() < money)
		{
			success = GatherResBoxes();
		}

		return success;
	}

	#region Gathering Strategy

	float GetGatheringCost()
	{
		var boxes = FindObjectsOfType<ResBox>();
		if (boxes.Length <= 0)
			return 0f;

		float closestDist = Vector3.Distance(transform.position, boxes[0].transform.position);
		foreach (var box in boxes)
		{
			float dist = Vector3.Distance(transform.position, box.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
			}
		}

		return closestDist * gameManager.transportCostPerMeter;
	}

	bool GatherResBoxes()
	{
		var boxes = FindObjectsOfType<ResBox>();
		if (boxes.Length <= 0)
		{
			target = transform.position;
			return false;
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
		target = closestBox.transform.position;
		target.y = origin.y;

		return true;
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
