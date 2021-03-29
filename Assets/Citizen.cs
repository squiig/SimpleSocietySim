using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Citizen : MonoBehaviour
{
	GameManager gameManager;
	Vector3 origin;
	Vector3 target;
	float timer;
	float totalSeconds;

	public TMP_Text moneyText;
	public float fieldRadius = 20f;
	public float speed = 5f;
	public float resBoxValuation = 0f; // starts out random, but can fluctuate by effect of profit margins
	public int totalResBoxesOwned; // the primary asset that can be found or bought
	public float money;
	public bool isDoomed; // meaning they dont have an existing cashflow + dont own assets + cant afford any type of investment right now

	[SerializeField]
	float lastSaleProfit;

	[SerializeField]
	float normalizedProfitTrend;

	public float MinimumResBoxSellingPrice => resBoxValuation / totalResBoxesOwned * 100;

	public bool AreProfitsIncreasing => normalizedProfitTrend > 0;

	public float TotalProfits => money - gameManager.citizenStartingCapital;

	public float ProfitPerSecond => TotalProfits / totalSeconds;

	bool TrySellResBox(int amount, float priceProposedByCustomer, Citizen customer)
	{
		// Considerations
		if (totalResBoxesOwned < amount)
			return false;

		if (priceProposedByCustomer < MinimumResBoxSellingPrice)
			return false;

		// Transaction
		totalResBoxesOwned -= amount;
		AddMoney(priceProposedByCustomer);
		customer.totalResBoxesOwned += amount;

		// Book keeping
		float profit = priceProposedByCustomer - MinimumResBoxSellingPrice;
		RegisterProfit(profit);

		return true;
	}

	void AddMoney(float money)
	{
		this.money += money;

		moneyText.text = $"${money:n2}";
	}

	void RegisterProfit(float profit)
	{
		normalizedProfitTrend = profit / (lastSaleProfit == 0 ? profit : lastSaleProfit) - 1;
		lastSaleProfit = profit;
	}

    // Start is called before the first frame update
    void Start()
	{
		gameManager = FindObjectOfType<GameManager>();

		timer = 1;

		origin = transform.position;
		target = origin;

		GetComponent<Renderer>().material.color = Random.ColorHSV(0, 1, .5f, 1, 1, 1, 1, 1);

		resBoxValuation = Random.value;

		moneyText.text = $"${money:n2}";
	}

	void Tick()
	{
		timer -= Time.deltaTime;

		if (timer <= 0)
		{
			timer = 1;
			totalSeconds++;
		}
	}

    // Update is called once per frame
    void Update()
    {
		Tick();

		// idling (free)
		if (Random.value < .001f)
		{
			origin = transform.position;
			target.x = Mathf.Clamp(origin.x + Random.Range(-1, 1) * 2, -fieldRadius, fieldRadius);
			target.z = Mathf.Clamp(origin.z + Random.Range(-1, 1) * 2, -fieldRadius, fieldRadius);
		}

		// look for profit, otherwise idle
		if (!AreProfitsIncreasing)
		{
			isDoomed = !TryBusinessStrategies();
		}

		if (Vector3.Distance(origin, target) > .01f)
			transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
	}

	bool TryBusinessStrategies()
	{
		bool success = false;

		success = GatherResBoxes();

		return success;
	}

	bool GatherResBoxes()
	{
		var boxes = FindObjectsOfType<ResBox>();
		if (boxes.Length <= 0)
			return false;

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

	void OnTriggerEnter(Collider collision)
	{
		var box = collision.gameObject.GetComponent<ResBox>();
		if (box)
		{
			totalResBoxesOwned++;
			Destroy(collision.gameObject);
		}
	}
}
