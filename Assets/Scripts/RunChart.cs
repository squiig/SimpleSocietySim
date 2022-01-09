using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI.Extensions;
using System.Linq;

public class RunChart : MonoBehaviour
{
	public float Width => (_xAxes.Count - 1) * _xAxesSpacing;
	public float Height => (_yAxes.Count - 1) * _yAxesSpacing;

	public float HighestValue => _chartPoints.Select(x => x.yValue).Max();
	public float LowestValue => _chartPoints.Select(x => x.yValue).Min();

	[SerializeField] private TMP_Text _titleLabel;
	[SerializeField] private string _title;

	[Header("Points")]
	[SerializeField] private GameObject _pointPrefab;
	[SerializeField] private GameObject _pointContainer;

	[Header("Connections")]
	[SerializeField] private UILineRenderer _lineRenderer;

	[Header("Axes")]
	[SerializeField] private List<GameObject> _xAxes;
	[SerializeField] private float _xAxesSpacing = 100;
	[SerializeField] private float _xAxesOffset = 50;
	[SerializeField] private List<GameObject> _yAxes;
	[SerializeField] private float _yAxesSpacing = 50;
	[SerializeField] private float _yAxesOffset = 0;

	[Header("Axis Markers")]
	[SerializeField, ReadOnly] private int _lowestXMarker;
	[SerializeField, ReadOnly] private int _highestXMarker;
	[SerializeField, ReadOnly] private int _xMarkerInterval;
	[SerializeField, ReadOnly] private int _lowestYMarker;
	[SerializeField, ReadOnly] private int _highestYMarker;
	[SerializeField, ReadOnly] private int _yMarkerInterval;

	private Queue<GameObject> _chartPointObjects;
	private Queue<ChartPoint> _chartPoints;

	public class ChartPoint
	{
		public float xValue, yValue, xPos, yPos;
		public Vector2 connectionPoint;
		public GameObject pointObject;
	}

	public void AddValue(float value)
	{
		ChartPoint chartPoint = new ChartPoint()
		{
			xValue = _highestXMarker + _xMarkerInterval,
			yValue = value,
			xPos = Width, // aka, on the right-most x-axis
			yPos = (value / _highestYMarker) * Height // percentage of the chart height
		};

		chartPoint.connectionPoint = ChartPosToConnectionPos(chartPoint.xPos, chartPoint.yPos);

		var go = Instantiate(_pointPrefab, _pointContainer.transform);
		chartPoint.pointObject = go;

		if (_chartPointObjects.Count > 0)
		{
			ShiftPointsLeft();
			ShiftXAxesLeft();
			//CondenseVertically();
		}

		_chartPoints.Enqueue(chartPoint);

		go.GetComponent<RectTransform>().anchoredPosition = new Vector2(chartPoint.xPos + _xAxesOffset, chartPoint.yPos + _yAxesOffset);
		_chartPointObjects.Enqueue(go);

		RefreshLineRendererPoints();
	}

	public void ConfigureXAxis(int min, int interval)
	{
		_lowestXMarker = min;
		_highestXMarker = min + interval * (_xAxes.Count - 1);
		_xMarkerInterval = interval;

		RefreshXAxisLabels();
	}

	public void ConfigureYAxis(int min, int max)
	{
		_lowestYMarker = min;
		_highestYMarker = Mathf.Max(max, 1);
		_yMarkerInterval = Mathf.CeilToInt((Mathf.Max(max, 1) - min) / (_yAxes.Count - 1));

		RefreshYAxisLabels();
	}

	void Awake()
	{
		_chartPointObjects = new Queue<GameObject>();
		_chartPoints = new Queue<ChartPoint>();
	}

	// Start is called before the first frame update
	void Start()
	{
		ClearPoints();

		_titleLabel.text = _title;

		RefreshXAxisLabels();
		RefreshYAxisLabels();
	}

	/// <summary>
	/// This conversion is neccessary because chart positions are anchored to the lower left corner, but connection positions are anchored to the center.
	/// </summary>
	Vector2 ChartPosToConnectionPos(float x, float y)
	{
		return new Vector2(
				//-((_xAxes.Count - 1) / 2 * _xAxesSpacing) + x,
				//-(Mathf.Floor(_yAxes.Count / 2) * _yAxesSpacing) + y
				x - 250,
				y - 100
			);
	}

	void RefreshLineRendererPoints()
	{
		_lineRenderer.Points = _chartPoints.Select(x => x.connectionPoint).ToArray();
	}

	void ShiftPointsLeft()
	{
		// Remove the left-most point if it goes off the chart
		if (_chartPoints.Count >= _xAxes.Count)
		{
			Destroy(_chartPointObjects.Dequeue());
			_chartPoints.Dequeue();
		}

		// Move all other points one x-axis to the left
		foreach (var point in _chartPoints)
		{
			point.xPos -= _xAxesSpacing;
			point.connectionPoint = ChartPosToConnectionPos(point.xPos, point.yPos);
			point.pointObject.GetComponent<RectTransform>().anchoredPosition += Vector2.left * _xAxesSpacing;
		}
	}

	void ShiftXAxesLeft()
	{
		ConfigureXAxis(_lowestXMarker + _xMarkerInterval, _xMarkerInterval);
		RefreshXAxisLabels();
	}

	void RefreshXAxisLabels()
	{
		for (int i = 0; i < _xAxes.Count; i++)
		{
			TMP_Text lineLabel = _xAxes[i].GetComponentInChildren<TMP_Text>();
			lineLabel.text = $"{Mathf.Min(_lowestXMarker + i * _xMarkerInterval, _highestXMarker)}";
		}
	}

	void RefreshYAxisLabels()
	{
		for (int i = 0; i < _yAxes.Count; i++)
		{
			TMP_Text lineLabel = _yAxes[i].GetComponentInChildren<TMP_Text>();
			lineLabel.text = $"{Mathf.Min(_lowestYMarker + i * _yMarkerInterval, _highestYMarker)}";
		}
	}

	void CondenseVertically()
	{
		// Markers
		int newHighestYMarker = Mathf.CeilToInt(HighestValue / _yMarkerInterval) * _yMarkerInterval;
		int newLowestYMarker = Mathf.FloorToInt(LowestValue / _yMarkerInterval) * _yMarkerInterval;
		ConfigureYAxis(newLowestYMarker, newHighestYMarker);
		RefreshYAxisLabels();

		// Chart points
		foreach (var point in _chartPoints)
		{
			point.yPos = (point.yValue / _highestYMarker) * Height;
			point.connectionPoint = ChartPosToConnectionPos(point.xPos, point.yPos);
			point.pointObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(point.xPos, point.yPos);
		}
	}

	void ClearPoints()
	{
		for (int i = 0; i < _pointContainer.transform.childCount; i++)
		{
			Destroy(_pointContainer.transform.GetChild(i).gameObject);
		}

		_chartPointObjects = new Queue<GameObject>();

		_lineRenderer.Points = new Vector2[] { };
	}
}
