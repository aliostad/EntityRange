using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using EntityRange.Data;

namespace EntityRange.Server.Controllers
{
	public class CarController : ApiController
	{

		[EnableRange]
		public IEnumerable<Car> Get()
		{
			return CarRepository.Instance.Get();
		}

		
	}
}