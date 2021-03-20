using DevExpress.Data.Filtering;
using DevExpress.Xpf.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace InfiniteSourceInMemoryData {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            var source = new InfiniteAsyncSource() {
                ElementType = typeof(IssueData)
            };
            Unloaded += (o, e) => {
                source.Dispose();
            };

            var getSourceTask = Task.Run(() => {
                return Enumerable
                    .Range(0, 10000000)
                    .Select(i => OutlookDataGenerator.CreateIssue())
                    .ToList();
            });

            source.FetchRows += (o, e) => {
                e.Result = FetchRows(e, getSourceTask);
            };

            source.GetTotalSummaries += (o, e) => {
                e.Result = GetTotalSummaries(e, getSourceTask);
            };

            source.GetUniqueValues += (o, e) => {
                if(e.PropertyName == "Priority") {
                    e.Result = Task.FromResult(Enum.GetValues(typeof(Priority)).Cast<object>().ToArray());
                } else {
                    throw new InvalidOperationException();
                }
            };

            grid.ItemsSource = source;
        }

        static async Task<FetchRowsResult> FetchRows(FetchRowsAsyncEventArgs e, Task<List<IssueData>> getSourceTask) {
            var enumerator = e.SkipToken as IEnumerator<IssueData>;
            if(enumerator == null) {
                var data = await getSourceTask;
                enumerator = Sort(data, e.SortOrder)
                    .Where(MakeFilterPredicate(e.Filter))
                    .GetEnumerator();
            }
            return await Task.Run(() => {
                var result = new List<IssueData>();
                int take = e.Take ?? 42;
                while(result.Count < take && enumerator.MoveNext()) {
                    result.Add(enumerator.Current);
                }

                return new FetchRowsResult(result.ToArray(), hasMoreRows: result.Count == take, nextSkipToken: enumerator);
            });
        }

        static async Task<object[]> GetTotalSummaries(GetSummariesAsyncEventArgs e, Task<List<IssueData>> getSourceTask) {
            if(e.Summaries.Single().SummaryType != SummaryType.Count)
                throw new NotImplementedException();
            var data = await getSourceTask;
            var count = await Task.Run(() => data.Where(MakeFilterPredicate(e.Filter)).Count());
            return new object[] { count };
        }


        static ParallelQuery<IssueData> Sort(List<IssueData> data, SortDefinition[] sortOrder) {
            var ordered = data.AsParallel();
            if(sortOrder.Any()) {
                var sort = sortOrder.Single();
                if(sort.PropertyName == "Created") {
                    if(sort.Direction == ListSortDirection.Ascending)
                        ordered = ordered.OrderBy(x => x.Created);
                    else
                        ordered = ordered.OrderByDescending(x => x.Created);
                } else if(sort.PropertyName == "Votes") {
                    if(sort.Direction == ListSortDirection.Ascending)
                        ordered = ordered.OrderBy(x => x.Votes);
                    else
                        ordered = ordered.OrderByDescending(x => x.Votes);
                } else {
                    throw new InvalidOperationException();
                }
            }
            return ordered;
        }
        static Func<IssueData, bool> MakeFilterPredicate(CriteriaOperator filter) {
            var converter = new GridFilterCriteriaToExpressionConverter<IssueData>();
            return converter.Convert(filter).Compile();
        }
    }
}
