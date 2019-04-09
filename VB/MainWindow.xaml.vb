Imports DevExpress.Data.Filtering
Imports DevExpress.Xpf.Data
Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows

Namespace InfiniteSourceInMemoryData
	Partial Public Class MainWindow
		Inherits Window

		Public Sub New()
			InitializeComponent()

			Dim source = New InfiniteAsyncSource() With {.ElementType = GetType(IssueData)}
			AddHandler Me.Unloaded, Sub(o, e)
				source.Dispose()
			End Sub

			Dim getSourceTask = Task.Run(Function()
				Return Enumerable.Range(0, 10000000).Select(Function(i) OutlookDataGenerator.CreateIssue()).ToList()
			End Function)

			AddHandler source.FetchRows, Sub(o, e)
				e.Result = FetchRows(e, getSourceTask)
			End Sub

			AddHandler source.GetTotalSummaries, Sub(o, e)
				e.Result = GetTotalSummaries(e, getSourceTask)
			End Sub

			AddHandler source.GetUniqueValues, Sub(o, e)
				If e.PropertyName = "Priority" Then
					e.Result = Task.FromResult(System.Enum.GetValues(GetType(Priority)).Cast(Of Object)().ToArray())
				Else
					Throw New InvalidOperationException()
				End If
			End Sub

			grid.ItemsSource = source
		End Sub

		Private Shared Async Function FetchRows(ByVal e As FetchRowsAsyncEventArgs, ByVal getSourceTask As Task(Of List(Of IssueData))) As Task(Of FetchRowsResult)
			Dim enumerator = TryCast(e.SkipToken, IEnumerator(Of IssueData))
			If enumerator Is Nothing Then
				Dim data = Await getSourceTask
				enumerator = Sort(data, e.SortOrder).Where(MakeFilterPredicate(e.Filter)).GetEnumerator()
			End If
			Return Await Task.Run(Function()
				Dim result = New List(Of IssueData)()
				Const pageSize As Integer = 42
				Do While enumerator.MoveNext() AndAlso result.Count < pageSize
					result.Add(enumerator.Current)
				Loop
				Return New FetchRowsResult(result.ToArray(), hasMoreRows:= result.Count = pageSize, nextSkipToken:= enumerator)
			End Function)
		End Function

		Private Shared Async Function GetTotalSummaries(ByVal e As GetSummariesAsyncEventArgs, ByVal getSourceTask As Task(Of List(Of IssueData))) As Task(Of Object())
			If e.Summaries.Single().SummaryType <> SummaryType.Count Then
				Throw New NotImplementedException()
			End If
			Dim data = Await getSourceTask
			Dim count = Await Task.Run(Function() data.Where(MakeFilterPredicate(e.Filter)).Count())
			Return New Object() { count }
		End Function


		Private Shared Function Sort(ByVal data As List(Of IssueData), ByVal sortOrder() As SortDefinition) As ParallelQuery(Of IssueData)
			Dim ordered = data.AsParallel()
			If sortOrder.Any() Then
'INSTANT VB NOTE: The local variable sort was renamed since Visual Basic will not allow local variables with the same name as their enclosing function or property:
				Dim sort_Renamed = sortOrder.Single()
				If sort_Renamed.PropertyName = "Created" Then
					If sort_Renamed.Direction = ListSortDirection.Ascending Then
						ordered = ordered.OrderBy(Function(x) x.Created)
					Else
						ordered = ordered.OrderByDescending(Function(x) x.Created)
					End If
				ElseIf sort_Renamed.PropertyName = "Votes" Then
					If sort_Renamed.Direction = ListSortDirection.Ascending Then
						ordered = ordered.OrderBy(Function(x) x.Votes)
					Else
						ordered = ordered.OrderByDescending(Function(x) x.Votes)
					End If
				Else
					Throw New InvalidOperationException()
				End If
			End If
			Return ordered
		End Function
		Private Shared Function MakeFilterPredicate(ByVal filter As CriteriaOperator) As Func(Of IssueData, Boolean)
			Dim converter = New GridFilterCriteriaToExpressionConverter(Of IssueData)()
			Return converter.Convert(filter).Compile()
		End Function
	End Class
End Namespace
