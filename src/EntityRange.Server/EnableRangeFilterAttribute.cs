using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace EntityRange.Server
{
	public class EnableRangeFilterAttribute : ActionFilterAttribute
	{
	
		private Type _elementType;
		private RangeHeaderValue _requestRangeHeader;
		private const string EnityRangeUnit = "x-entity";

	

		public override void OnActionExecuting(HttpActionContext actionContext)
		{
			if(!actionContext.ActionDescriptor.ReturnType.IsGenericType ||
				actionContext.ActionDescriptor.ReturnType.GetGenericTypeDefinition()!=typeof(IEnumerable<>))
			{
				throw new InvalidOperationException("Return type must be IEnumerable<T>.");
			}
			else
			{
				_elementType = actionContext.ActionDescriptor.ReturnType.GetGenericArguments()[0];
			}

			_requestRangeHeader = actionContext.Request.Headers.Range;

			if (_requestRangeHeader!=null && _requestRangeHeader.Unit != EnityRangeUnit)
				throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable));

			base.OnActionExecuting(actionContext);
		}

		public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
		{
			if(actionExecutedContext.Exception==null && actionExecutedContext.Response.IsSuccessStatusCode)
			{

				if(_requestRangeHeader == null)
				{
					// if no header, put Accept range header to tell teh client it is supported 
					actionExecutedContext.Response.Headers.Add("Accept-Ranges", EnityRangeUnit);
				}
				else
				{

					var objectContent = actionExecutedContext.Response.Content as ObjectContent;
					if(objectContent!=null)
					{

						var value = objectContent.Value;
						var t = typeof(Enumerable);
						var rangeItemHeaderValue = _requestRangeHeader.Ranges.First(); // only support one range 
						var skipMethod = t.GetMethods().Where(m => m.Name == "Skip" && m.GetParameters().Count() == 2)
							.First().MakeGenericMethod(_elementType);
						var takeMethod = t.GetMethods().Where(m => m.Name == "Take" && m.GetParameters().Count() == 2)
							.First().MakeGenericMethod(_elementType);


						// calculate and add header 
						var from = (int)rangeItemHeaderValue.From;
						var to = (int?) rangeItemHeaderValue.To;
						string toString = to.HasValue ? to.Value.ToString() : "*";
						string countString = "*";
						var collection = value as ICollection; // if underlying a collection we can find out count without iterating 
						if (collection != null)
						{
							to = to.HasValue ? Math.Min(to.Value, collection.Count-1) : collection.Count-1;
							toString = to.ToString();
							countString = collection.Count.ToString();
						}
						actionExecutedContext.Response.Content.Headers.Add("content-range",
							string.Format("{0} {1}-{2}/{3}", EnityRangeUnit, from, toString, countString));

						// invoke 
						value = skipMethod.Invoke(null, new object[] { value,  from});
						value = takeMethod.Invoke(null, new object[] { value, to - from + 1 });
						objectContent.Value = value;
						
						// set status to 206
						actionExecutedContext.Response.StatusCode = HttpStatusCode.PartialContent;
					}
					
				}
				
			}

			base.OnActionExecuted(actionExecutedContext);
		}
	}
}