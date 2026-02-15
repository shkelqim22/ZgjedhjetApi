using Elasticsearch.Net;
using Microsoft.EntityFrameworkCore;
using Nest;
using StackExchange.Redis;
using System.Text.Json.Serialization;
using ZgjedhjetApi.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── SQL Server (Entity Framework Core) ───────────────────────────────────────
builder.Services.AddDbContext<LifeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LifeDatabase")));

// ── Elasticsearch (NEST) ──────────────────────────────────────────────────────
var esUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
var connectionSettings = new ConnectionSettings(new Uri(esUri))
    .DefaultIndex("zgjedhjet");
builder.Services.AddSingleton<IElasticClient>(new ElasticClient(connectionSettings));

// ── Redis ─────────────────────────────────────────────────────────────────────
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

























/*
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json.Serialization;
using ZgjedhjetApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.



builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddDbContext<LifeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LifeDatabase")));

var esUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
var esSettings = new ElasticsearchClientSettings(new Uri(esUri))
    .ServerCertificateValidationCallback((sender, cert, chain, errors) => true)
    .Authentication(new BasicAuthentication("elastic", "dbUx+Xw+CUwy8GvXaV-H"))
    .DefaultIndex("zgjedhjet");

builder.Services.AddSingleton(new ElasticsearchClient(esSettings));

var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();*/
