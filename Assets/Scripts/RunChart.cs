using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI.Extensions;
using UnityEditor;
using System.Linq;

/// <summary>
/// A chart that displays data over time.
/// </summary>
public class RunChart : MonoBehaviour
{
	public float Width => (_xAxes.Count - 1) * _xAxesSpacing;
	public float Height => (_yAxes.Count - 1) * _yAxesSpacing;

	public float HighestValue => _chartPoints.Select(x => x.YValue).Max();
	public float LowestValue => _chartPoints.Select(x => x.YValue).Min();

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
	[SerializeField, ReadOnly] private float _lowestXMarker;
	[SerializeField, ReadOnly] private float _highestXMarker;
	[SerializeField, ReadOnly] private float _xMarkerInterval;
	[SerializeField] private string _xMarkerPrefix;
	[SerializeField] private string _xMarkerSuffix;
	[SerializeField, ReadOnly] private float _lowestYMarker;
	[SerializeField, ReadOnly] private float _highestYMarker;
	[SerializeField, ReadOnly] private float _yMarkerInterval;
	[SerializeField] private string _yMarkerPrefix;
	[SerializeField] private string _yMarkerSuffix;

	private Queue<ChartPoint> _chartPoints;

	void Awake()
	{
		_chartPoints = new Queue<ChartPoint>();
	}

	// Start is called before the first frame update
	void Start()
	{
		ClearPoints();

		_titleLabel.text = _title;

		ConfigureXAxis(-_xAxes.Count + 1, 1);
		ConfigureYAxis(0, 100);
	}

	public void AddValue(float value)
	{
		var chartPoint = CreateChartPoint(value);

		if (_chartPoints.Count > 0)
		{
			ShiftChartLeft();
		}

		_chartPoints.Enqueue(chartPoint);

		CondenseVertically();
	}

	public void ConfigureXAxis(float min, float interval)
	{
		_lowestXMarker = min;
		_highestXMarker = min + interval * (_xAxes.Count - 1);
		_xMarkerInterval = interval;

		RefreshXAxisLabels();
	}

	public void ConfigureYAxis(float min, float max)
	{
		_lowestYMarker = min;
		_highestYMarker = Mathf.Max(max, 1);

		float yMarkerInterval = (Mathf.Max(max, 1) - min) / (_yAxes.Count - 1);

		_yMarkerInterval = Mathf.Max(.25f, yMarkerInterval);//Mathf.CeilToInt(yMarkerInterval));

		RefreshYAxisLabels();
	}

	ChartPoint CreateChartPoint(float value)
	{
		float xValue = _highestXMarker + _xMarkerInterval;
		float yValue = value;
		float xPos = Width; // aka, on the right-most x-axis
		var go = Instantiate(_pointPrefab, _pointContainer.transform);
		var point = new ChartPoint(this, xValue, yValue, xPos, 0, Vector2.zero, go);
		RepositionChartPoint(point);
		return point;
	}

	void RepositionChartPoint(ChartPoint chartPoint)
	{
		chartPoint.YPos = (chartPoint.YValue / _highestYMarker) * Height; // percentage of the chart height
		Vector2 anchoredPos = new Vector2(chartPoint.XPos + _xAxesOffset, chartPoint.YPos + _yAxesOffset);
		chartPoint.PointObject.GetComponent<RectTransform>().anchoredPosition = anchoredPos;
		chartPoint.ConnectionPoint = ChartPosToConnectionPos(anchoredPos.x, anchoredPos.y);
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
		_lineRenderer.Points = _chartPoints.Select(x => x.ConnectionPoint).ToArray();
	}

	void ShiftChartLeft()
	{
		ShiftPointsLeft();
		ShiftXAxesLeft();
	}

	void ShiftPointsLeft()
	{
		// Remove the left-most point if it goes off the chart
		if (_chartPoints.Count >= _xAxes.Count)
		{
			var chartPoint = _chartPoints.Dequeue();
			Destroy(chartPoint.PointObject);
		}

		// Move all other points one x-axis to the left
		foreach (var point in _chartPoints)
		{
			point.XPos -= _xAxesSpacing;
			point.ConnectionPoint = ChartPosToConnectionPos(point.XPos, point.YPos);
			point.PointObject.GetComponent<RectTransform>().anchoredPosition += Vector2.left * _xAxesSpacing;
		}
	}

	void ShiftXAxesLeft()
	{
		ConfigureXAxis(_lowestXMarker + _xMarkerInterval, _xMarkerInterval);
		RefreshXAxisLabels();
	}

	void RefreshXAxisLabels()
	{
		int len = _xAxes.Count;
		for (int i = 0; i < len; i++)
		{
			TMP_Text lineLabel = _xAxes[i].GetComponentInChildren<TMP_Text>();
			lineLabel.text = $"{_xMarkerPrefix}{(i == len - 1 ? _highestXMarker : _lowestXMarker + i * _xMarkerInterval)}{_xMarkerSuffix}";
		}
	}

	void RefreshYAxisLabels()
	{
		int len = _yAxes.Count;
		for (int i = 0; i < len; i++)
		{
			TMP_Text lineLabel = _yAxes[i].GetComponentInChildren<TMP_Text>();
			lineLabel.text = $"{_yMarkerPrefix}{(i == len - 1 ? _highestYMarker : _lowestYMarker + i * _yMarkerInterval):0.##}{_yMarkerSuffix}";
		}
	}

	void CondenseVertically()
	{
		// Markers
		int newHighestYMarker = (int)((Mathf.RoundToInt(HighestValue / _yMarkerInterval) + (int)Mathf.Sign(HighestValue)) * _yMarkerInterval);

		if (newHighestYMarker > 1)
		{
			int i = (int)Mathf.Pow(10, Mathf.FloorToInt(Mathf.Log10(newHighestYMarker)));
			newHighestYMarker = newHighestYMarker - (newHighestYMarker % i) + i;
		}

		int newLowestYMarker = (int)Mathf.Min(0, (Mathf.RoundToInt(LowestValue / _yMarkerInterval) + (int)Mathf.Sign(LowestValue)) * _yMarkerInterval);

		ConfigureYAxis(newLowestYMarker, newHighestYMarker);

		// Chart points
		foreach (var point in _chartPoints)
		{
			RepositionChartPoint(point);
		}

		RefreshLineRendererPoints();
	}

	void ClearPoints()
	{
		for (int i = 0; i < _pointContainer.transform.childCount; i++)
		{
			Destroy(_pointContainer.transform.GetChild(i).gameObject);
		}

		_chartPoints = new Queue<ChartPoint>();
		_lineRenderer.Points = new Vector2[] { };
	}

	public class ChartPoint
	{
		private RunChart _runChart;
		private float _xValue, _yValue;
		private float _xPos, _yPos;
		private Vector2 _connectionPoint;
		private GameObject _pointObject;

		public ChartPoint(RunChart runChart, float xValue, float yValue, float xPos, float yPos, Vector2 connectionPoint, GameObject pointObject)
		{
			_runChart = runChart;
			_xValue = xValue;
			_yValue = yValue;
			_xPos = xPos;
			_yPos = yPos;
			_connectionPoint = connectionPoint;
			_pointObject = pointObject;
		}

		public RunChart RunChart { get => _runChart; set => _runChart = value; }
		public float XValue { get => _xValue; set => _xValue = value; }
		public float YValue { get => _yValue; set => _yValue = value; }
		public float XPos { get => _xPos; set => _xPos = value; }
		public float YPos { get => _yPos; set => _yPos = value; }
		public Vector2 ConnectionPoint { get => _connectionPoint; set => _connectionPoint = value; }
		public GameObject PointObject { get => _pointObject; set => _pointObject = value; }
	}
}
