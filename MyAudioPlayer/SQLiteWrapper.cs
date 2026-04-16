using Microsoft.Data.Sqlite;
using MyAudioPlayer;
using System;
using System.Collections.Generic;


//比較演算子を受け渡すためのenum
public enum ComparisonOperatorEnum{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    Like,
    NotLike
}
public struct ComparisonOperator{
    public ComparisonOperatorEnum Operator { get; init; }
    public ComparisonOperator(ComparisonOperatorEnum op){
        Operator = op;
    }
    static public ComparisonOperator Equal => new ComparisonOperator(ComparisonOperatorEnum.Equal);
    static public ComparisonOperator NotEqual => new ComparisonOperator(ComparisonOperatorEnum.NotEqual);
    static public ComparisonOperator GreaterThan => new ComparisonOperator(ComparisonOperatorEnum.GreaterThan);
    static public ComparisonOperator LessThan => new ComparisonOperator(ComparisonOperatorEnum.LessThan);
    static public ComparisonOperator GreaterOrEqual => new ComparisonOperator(ComparisonOperatorEnum.GreaterOrEqual);
    static public ComparisonOperator LessOrEqual => new ComparisonOperator(ComparisonOperatorEnum.LessOrEqual);
    static public ComparisonOperator Like => new ComparisonOperator(ComparisonOperatorEnum.Like);
    static public ComparisonOperator NotLike => new ComparisonOperator(ComparisonOperatorEnum.NotLike);

    public string GetString(){
        return Operator switch{
            ComparisonOperatorEnum.Equal => "=",
            ComparisonOperatorEnum.NotEqual => "!=",
            ComparisonOperatorEnum.GreaterThan => ">",
            ComparisonOperatorEnum.LessThan => "<",
            ComparisonOperatorEnum.GreaterOrEqual => ">=",
            ComparisonOperatorEnum.LessOrEqual => "<=",
            ComparisonOperatorEnum.Like => "LIKE",
            ComparisonOperatorEnum.NotLike => "NOT LIKE",
            _ => throw new InvalidOperationException("Invalid comparison operator")
        };
    }
}

public class SQLiteWrapperTestCode {
    private static void TestCode1() {
        //データベースへのアクセスのテスト
        var SQL = new SQLiteWrapper("MyAudioPlayer.db");
        SQL.SetMainTable("CVinfo1");
        SQL.AddWhere("characterName", "塩見周子");
        var results = SQL.Select();
        foreach (var row in results){
            MainWindow.Log($"CVInfoID: {row["CVInfoID"]}, Character: {row["characterName"]}, CV: {row["CVName"]}");
        }
    }
}

public class SQLiteWrapper : IDisposable{
    private readonly string _dbPath;
    private SqliteConnection? _dbConnection;
    private SqliteTransaction? _dbTransaction;
    private List<string> _whereWithPlaceholder=new();
    private Dictionary<string, object> _placeholdersToValues=new();
    private string _mainTableName="";
    private List<string> _tablesToJoin=new();

    public SQLiteWrapper(string dbPath){
        _dbPath=dbPath;
    }
    // --- 接続管理 ---
    public void Open(){
        if (_dbConnection == null) {
            _dbConnection = new SqliteConnection($"Data Source={_dbPath}");
        }
        if (_dbConnection.State != System.Data.ConnectionState.Open){
            _dbConnection.Open();
        }
    }
    public void Close(){
        _dbTransaction?.Dispose();
        _dbTransaction = null;
        _dbConnection?.Close();
        _dbConnection?.Dispose();
        _dbConnection = null;
    }
    // --- トランザクション (データ不整合防止) ---
    public void BeginTransaction(){
        Open();
        _dbTransaction = _dbConnection!.BeginTransaction();
    }
    public void Commit(){
        _dbTransaction?.Commit();
        _dbTransaction = null;
    }
    public void Rollback(){
        _dbTransaction?.Rollback();
        _dbTransaction = null;
    }
    public void Dispose(){
        _dbConnection?.Close();
        _dbConnection?.Dispose();
    }
    // --- WHERE句の管理 ---
    public void AddWhere(string columnName, object value, ComparisonOperatorEnum op=ComparisonOperatorEnum.Equal){
        string placeholder = $"@{columnName}_{_placeholdersToValues.Count}";
        _whereWithPlaceholder.Add($"{columnName} {new ComparisonOperator(op).GetString()} {placeholder}");
        _placeholdersToValues[placeholder] = value;
    }
    public void AddWhereIn(string columnName, List<object> values){
        if (values == null || values.Count == 0) return;

        var placeholdersList = new List<string>();
        for (int i = 0; i < values.Count; i++){
            string placeholder = $"@{columnName}_in_{_placeholdersToValues.Count}";
            placeholdersList.Add(placeholder);
            _placeholdersToValues[placeholder] = values[i];
        }
        _whereWithPlaceholder.Add($"{columnName} IN ({string.Join(", ", placeholdersList)})");
    }
    public void ClearWhere(){
        _whereWithPlaceholder.Clear();
        _placeholdersToValues.Clear();
    }
    public string GetWhereClause(){
        if(_whereWithPlaceholder.Count == 0){
            return "";
        }else{
            return "WHERE " + string.Join(" AND ", _whereWithPlaceholder);
        }
    }

    // --- JOIN句の管理 ---
    public void SetMainTable(string tableName){
        _mainTableName = tableName;
    }
    public void AddJoin(string tableName,string joinType,string baseTable,string baseColumn,string joinColumn){
        _tablesToJoin.Add($"{joinType} JOIN {tableName} ON {baseTable}.{baseColumn} = {tableName}.{joinColumn}");
    }
    public string GetJoinClause(){
        if(_tablesToJoin.Count == 0){
            return "";
        }else{
            return string.Join(" ", _tablesToJoin);
        }
    }

    //クエリを送信するSqliteCommandの実行処理3種
    private int ExecuteNonQuery(string sql, Dictionary<string, object> columnsToValues){
        using var command = new SqliteCommand(sql, _dbConnection, _dbTransaction);
        int index = 0;
        foreach (var kvp in columnsToValues){
            command.Parameters.AddWithValue($"@param{index}", kvp.Value ?? DBNull.Value);
            index++;
        }
        foreach (var kvp in _placeholdersToValues){
            command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
        }
        return command.ExecuteNonQuery();
    }
    private object? ExecuteScalar(string sql, Dictionary<string, object> columnsToValues){
        using var command = new SqliteCommand(sql, _dbConnection, _dbTransaction);
        int index = 0;
        foreach (var kvp in columnsToValues){
            command.Parameters.AddWithValue($"@param{index}", kvp.Value ?? DBNull.Value);
            index++;
        }
        foreach (var kvp in _placeholdersToValues){
            command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
        }
        return command.ExecuteScalar();
    }
    private List<Dictionary<string, object>> ExecuteReader(string sql, Dictionary<string, object> columnsToValues){
        using var command = new SqliteCommand(sql, _dbConnection, _dbTransaction);
        int index = 0;
        foreach (var kvp in columnsToValues){
            command.Parameters.AddWithValue($"@param{index}", kvp.Value ?? DBNull.Value);
            index++;
        }
        foreach (var kvp in _placeholdersToValues){
            command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
        }
        using var reader = command.ExecuteReader();
        //結果の書き出し
        var results = new List<Dictionary<string, object>>();
        while (reader.Read()){
            var row = new Dictionary<string, object>();
            for (int field = 0; field < reader.FieldCount; field++){
                row[reader.GetName(field)] = reader.GetValue(field);
            }
            results.Add(row);
        }
        return results;
    }

    //最終挿入行のIDを返すメソッド
    public int GetLastInsertRowId(){
        Open();
        using var command = new SqliteCommand("SELECT last_insert_rowid();", _dbConnection, _dbTransaction);
        return (int)command.ExecuteScalar()!;
    }

    //テーブル名確認のガード節
    private void EnsureMainTableSet(string? tableName){
        if (tableName != null){
            SetMainTable(tableName);
        }else if (string.IsNullOrEmpty(_mainTableName)){
            throw new InvalidOperationException("Main table name is not set. Please provide a table name or set it using SetMainTable method.");
        }
    }

    //基本機能
    public List<Dictionary<string,object>> Select(string[]? columns=null, string? tableName=null){
        EnsureMainTableSet(tableName);
        Open();
        string columnsPart;
        if(columns==null || columns.Length == 0){
            columnsPart="*";
        }else{
            columnsPart=string.Join(", ", columns);
        }
        string joinPart=GetJoinClause();
        string wherePart=GetWhereClause();
        string sql = $"SELECT {columnsPart} FROM {_mainTableName} {joinPart} {wherePart};";
        return ExecuteReader(sql, new Dictionary<string, object>());
    }
    //集計用の、ExecuteSchalarを使う場合の処理
    public object Aggregate(string aggregateFunction, string columnName, string? tableName = null){
        EnsureMainTableSet(tableName);
        Open();
        string joinPart = GetJoinClause();
        string wherePart =GetWhereClause();
        string sql = $"SELECT {aggregateFunction}({columnName}) FROM {tableName} {joinPart} {wherePart};";
        return ExecuteScalar(sql,new Dictionary<string,object>()) ?? throw new InvalidOperationException("Aggregate function returned null");
    }

    public int Insert(Dictionary<string, object> columnsToValues, string? tableName = null){
        EnsureMainTableSet(tableName);
        Open();
        string columnsPart = string.Join(", ", columnsToValues.Keys);
        string valuesPart = string.Join(", ", columnsToValues.Keys.Select((column, index) => $"@param{index}"));
        string sql = $"INSERT INTO {tableName} ({columnsPart}) VALUES ({valuesPart});";
        ExecuteNonQuery(sql, columnsToValues);
        return GetLastInsertRowId();
    }
    public int Update(Dictionary<string, object> columnsToValues, string? tableName = null){
        EnsureMainTableSet(tableName);
        Open();
        string setPart = string.Join(", ", columnsToValues.Keys.Select((column, index) => $"{column} = @param{index}"));
        string wherePart=GetWhereClause();
        string sql = $"UPDATE {tableName} SET {setPart} {wherePart};";
        return ExecuteNonQuery(sql, columnsToValues);
    }
    public int Delete(string? tableName){
        EnsureMainTableSet(tableName);
        Open();
        string wherePart =GetWhereClause();
        string sql = $"DELETE FROM {tableName} {wherePart};";
        return ExecuteNonQuery(sql, new Dictionary<string, object>());
    }
    public int Upsert(Dictionary<string, object> columnsToValues, string? tableName = null){
        EnsureMainTableSet(tableName);
        Open();
        string columnsPart = string.Join(", ", columnsToValues.Keys);
        string wherePart=GetWhereClause();
        string sql = $"SELECT 1 FROM {tableName} {wherePart} LIMIT 1;";
        var result= ExecuteReader(sql, new Dictionary<string, object>());
        if (result.Count == 0) {
            return Insert(columnsToValues);
        } else {
            return Update(columnsToValues);
        }
    }


}