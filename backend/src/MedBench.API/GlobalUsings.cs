global using Microsoft.AspNetCore.Mvc;
global using Microsoft.Azure.Cosmos;
global using Microsoft.Identity.Web;
global using Microsoft.OpenApi.Models;
global using Microsoft.AspNetCore.Authorization;
global using MedBench.Core.Interfaces;
global using MedBench.Core.Models;
global using CoreUser = MedBench.Core.Models.User;
global using System.Security.Claims; 
global using System.Security.Principal;  // Add this for ClaimsIdentity
global using MongoDB.Bson;
global using MongoDB.Driver;