using IoTPlatform.Data;
using IoTPlatform.Models;
using IoTPlatform.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Web.Controllers;

/// <summary>产品与物模型</summary>
[ApiController]
[Route("api/products")]
[Tags("Products & thing model")]
public class ProductsController(ProductService service) : ControllerBase
{
    /// <summary>产品列表</summary>
    [HttpGet]
    public PagedResult<Product> Query(String? keyword, String? protocol, Int32 pageIndex = 1, Int32 pageSize = 20) => service.Query(keyword, protocol, pageIndex, pageSize);

    [HttpGet("{id:int}")]
    public Product Get(Int32 id) => service.Get(id);

    [HttpPost]
    public ActionResult<Product> Create(ProductInput input)
    {
        var product = service.Create(input);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public Product Update(Int32 id, ProductInput input) => service.Update(id, input);

    [HttpDelete("{id:int}")]
    public IActionResult Delete(Int32 id)
    {
        service.Delete(id);
        return NoContent();
    }

    /// <summary>物模型（属性、事件、服务）</summary>
    [HttpGet("{id:int}/thing-model")]
    public Object GetThingModel(Int32 id)
    {
        service.Get(id);
        return new
        {
            properties = service.GetProperties(id),
            events = service.GetEvents(id).Select(e => new { e.Id, e.Identifier, e.Name, e.Type, e.Required, e.FunctionBlockId, OutputData = ProductService.ParseParameters(e.OutputData), e.Sort, e.Enable, e.Remark }),
            services = service.GetServices(id).Select(e => new { e.Id, e.Identifier, e.Name, e.CallType, e.Required, e.FunctionBlockId, InputData = ProductService.ParseParameters(e.InputData), OutputData = ProductService.ParseParameters(e.OutputData), e.Sort, e.Enable, e.Remark }),
            functionBlocks = IoTPlatform.ThingModel.TslDocument.ParseCatalog(service.Get(id).FunctionBlocks),
        };
    }

    /// <summary>导出阿里云 TSL JSON。block 指定自定义模块；full=true 时嵌套各模块功能，便于在本平台往返</summary>
    [HttpGet("{id:int}/tsl")]
    public ContentResult ExportTsl(Int32 id, String? block = null, Boolean full = false) =>
        Content(service.ExportTsl(id, block, full), "application/json");

    /// <summary>导入阿里云 TSL 或 NewLife.IoT ThingSpec JSON（按模块和标识符新增或覆盖）</summary>
    [HttpPost("{id:int}/tsl")]
    [Consumes("application/json", "text/plain")]
    public async Task<Object> ImportTsl(Int32 id)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        var (p, e, s) = service.ImportModel(id, json);
        return new { properties = p, events = e, services = s };
    }

    /// <summary>导出 NewLife.IoT ThingSpec 物模型 Json</summary>
    [HttpGet("{id:int}/thing-spec")]
    public ContentResult ExportSpec(Int32 id) => Content(service.GetSpecification(id).ToJson(), "application/json");

    /// <summary>导入 NewLife.IoT ThingSpec 物模型 Json（按标识符新增或覆盖）</summary>
    [HttpPost("{id:int}/thing-spec")]
    [Consumes("application/json", "text/plain")]
    public async Task<Object> ImportSpec(Int32 id)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        var (p, e, s) = service.ImportSpecification(id, json);

        return new { properties = p, events = e, services = s };
    }

    [HttpPost("{id:int}/properties")]
    public ThingProperty AddProperty(Int32 id, ThingPropertyInput input) => service.SaveProperty(id, 0, input);

    [HttpPut("{id:int}/properties/{propertyId:int}")]
    public ThingProperty UpdateProperty(Int32 id, Int32 propertyId, ThingPropertyInput input) => service.SaveProperty(id, propertyId, input);

    [HttpDelete("{id:int}/properties/{propertyId:int}")]
    public IActionResult DeleteProperty(Int32 id, Int32 propertyId)
    {
        service.DeleteProperty(id, propertyId);
        return NoContent();
    }

    [HttpPost("{id:int}/events")]
    public ThingEvent AddEvent(Int32 id, ThingEventInput input) => service.SaveEvent(id, 0, input);

    [HttpPut("{id:int}/events/{eventId:int}")]
    public ThingEvent UpdateEvent(Int32 id, Int32 eventId, ThingEventInput input) => service.SaveEvent(id, eventId, input);

    [HttpDelete("{id:int}/events/{eventId:int}")]
    public IActionResult DeleteEvent(Int32 id, Int32 eventId)
    {
        service.DeleteEvent(id, eventId);
        return NoContent();
    }

    [HttpPost("{id:int}/services")]
    public ThingService AddService(Int32 id, ThingServiceInput input) => service.SaveService(id, 0, input);

    [HttpPut("{id:int}/services/{serviceId:int}")]
    public ThingService UpdateService(Int32 id, Int32 serviceId, ThingServiceInput input) => service.SaveService(id, serviceId, input);

    [HttpDelete("{id:int}/services/{serviceId:int}")]
    public IActionResult DeleteService(Int32 id, Int32 serviceId)
    {
        service.DeleteService(id, serviceId);
        return NoContent();
    }
}
