Imports DevExpress.Data.Filtering
Imports DevExpress.Xpf.Data
Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows

Namespace InfiniteSourceInMemoryData

    Public Partial Class MainWindow
        Inherits Window

        Public Sub New()
            Me.InitializeComponent()
            Dim source = New InfiniteAsyncSource() With {.ElementType = GetType(IssueData)}
            AddHandler Unloaded, Sub(o, e) source.Dispose()
            Dim getSourceTask = Task.Run(Function() Enumerable.Range(0, 10000000).[Select](Function(i) CreateIssue()).ToList())
            AddHandler source.FetchRows, Sub(o, e) e.Result = FetchRows(e, getSourceTask)
            AddHandler source.GetTotalSummaries, Sub(o, e) e.Result = GetTotalSummaries(e, getSourceTask)
            AddHandler source.GetUniqueValues, Sub(o, e)
                If Equals(e.PropertyName, "Priority") Then
                    e.Result = Task.FromResult([Enum].GetValues(GetType(Priority)).Cast(Of Object)().ToArray())
                Else
                    Throw New InvalidOperationException()
                End If
            End Sub
            Me.grid.ItemsSource = source
        End Sub

        Private Shared Async Function FetchRows(ByVal e As FetchRowsAsyncEventArgs, ByVal getSourceTask As Task(Of List(Of IssueData))) As Task(Of FetchRowsResult)
            Dim enumerator = TryCast(e.SkipToken, IEnumerator(Of IssueData))
            If enumerator Is Nothing Then
                Dim data = Await getSourceTask
                enumerator = ParallelEnumerable.Where(Sort(data, e.SortOrder), MakeFilterPredicate(e.Filter)).GetEnumerator()
            End If

            Return Await Task.Run(Function()
                Dim result = New List(Of IssueData)()
                Dim take As Integer = If(e.Take, 42)
                While result.Count < take AndAlso enumerator.MoveNext()
                    result.Add(enumerator.Current)
                End While

                Return New FetchRowsResult(result.ToArray(), hasMoreRows:=result.Count = take, nextSkipToken:=enumerator)
            End Function)
        End Function

        Private Shared Async Function GetTotalSummaries(ByVal e As GetSummariesAsyncEventArgs, ByVal getSourceTask As Task(Of List(Of IssueData))) As Task(Of Object())
            If Enumerable.Single(e.Summaries).SummaryType <> SummaryType.Count Then Throw New NotImplementedException()
            Dim data = Await getSourceTask
            Dim count = Await Task.Run(Function() data.Where(MakeFilterPredicate(e.Filter)).Count())
            Return New Object() {count}
        End Function

        Private Shared Function Sort(ByVal data As List(Of IssueData), ByVal sortOrder As SortDefinition()) As ParallelQuery(Of IssueData)
            Dim ordered = data.AsParallel()
            If sortOrder.Any() Then
                Dim lSort = sortOrder.[Single]()
                If Equals(lSort.PropertyName, "Created") Then
                    If lSort.Direction = ListSortDirection.Ascending Then
                        ordered = ordered.OrderBy(Function(x) x.Created)
                    Else
                        ordered = ordered.OrderByDescending(Function(x) x.Created)
                    End If
                ElseIf Equals(lSort.PropertyName, "Votes") Then
                    If lSort.Direction = ListSortDirection.Ascending Then
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
