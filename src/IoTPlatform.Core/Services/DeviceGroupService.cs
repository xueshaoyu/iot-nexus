using IoTPlatform.Data;
using IoTPlatform.Models;
using NewLife;
using XCode;

namespace IoTPlatform.Services;

/// <summary>树形分组节点</summary>
public record GroupNode(DeviceGroup Group, Int32 Level, Int32 Devices, Int32 Onlines, Int32 TotalDevices, Int32 TotalOnlines);

/// <summary>设备分组管理</summary>
public class DeviceGroupService
{
    public IList<DeviceGroup> GetAll() => DeviceGroup.FindAll().OrderBy(e => e.ParentId).ThenBy(e => e.Sort).ThenBy(e => e.Id).ToList();

    public DeviceGroup Get(Int32 id) => DeviceGroup.FindById(id) ?? throw PlatformException.NotFound("Group");

    /// <summary>按树形顺序展开所有分组，附带直属与含子分组的设备数、在线数</summary>
    public IList<GroupNode> GetTree()
    {
        var groups = GetAll();
        var devices = Device.FindAll();
        var direct = devices.GroupBy(e => e.GroupId).ToDictionary(g => g.Key, g => (Total: g.Count(), Online: g.Count(d => d.Online)));

        var result = new List<GroupNode>();
        void Walk(Int32 parentId, Int32 level)
        {
            foreach (var g in groups.Where(e => e.ParentId == parentId))
            {
                var ids = GetDescendantIds(g.Id, groups);
                ids.Add(g.Id);
                var own = direct.GetValueOrDefault(g.Id);
                var total = ids.Sum(id => direct.GetValueOrDefault(id).Total);
                var onlines = ids.Sum(id => direct.GetValueOrDefault(id).Online);
                result.Add(new GroupNode(g, level, own.Total, own.Online, total, onlines));
                Walk(g.Id, level + 1);
            }
        }
        Walk(0, 0);

        // 父级丢失的孤儿分组挂到根
        foreach (var g in groups.Where(e => e.ParentId > 0 && groups.All(p => p.Id != e.ParentId)))
        {
            var own = direct.GetValueOrDefault(g.Id);
            result.Add(new GroupNode(g, 0, own.Total, own.Online, own.Total, own.Online));
        }

        return result;
    }

    /// <summary>获取所有子孙分组编号</summary>
    public static HashSet<Int32> GetDescendantIds(Int32 groupId, IList<DeviceGroup>? groups = null)
    {
        groups ??= DeviceGroup.FindAll();
        var set = new HashSet<Int32>();
        var queue = new Queue<Int32>([groupId]);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var child in groups.Where(e => e.ParentId == id))
            {
                if (set.Add(child.Id)) queue.Enqueue(child.Id);
            }
        }

        return set;
    }

    /// <summary>groupId 是否为 ancestorId 的子孙</summary>
    public static Boolean IsDescendant(Int32 groupId, Int32 ancestorId)
    {
        var visited = new HashSet<Int32>();
        var g = DeviceGroup.FindById(groupId);
        while (g != null && g.ParentId > 0 && visited.Add(g.Id))
        {
            if (g.ParentId == ancestorId) return true;
            g = DeviceGroup.FindById(g.ParentId);
        }

        return false;
    }

    /// <summary>分组完整路径，如 "Factory A / Line 1"</summary>
    public static String GetPath(Int32 groupId)
    {
        var names = new List<String>();
        var visited = new HashSet<Int32>();
        var g = DeviceGroup.FindById(groupId);
        while (g != null && visited.Add(g.Id))
        {
            names.Insert(0, g.Name);
            g = g.ParentId > 0 ? DeviceGroup.FindById(g.ParentId) : null;
        }

        return String.Join(" / ", names);
    }

    public DeviceGroup Create(GroupInput input)
    {
        var group = new DeviceGroup();
        Apply(group, input);
        group.Insert();

        return group;
    }

    public DeviceGroup Update(Int32 id, GroupInput input)
    {
        var group = Get(id);
        if (input.ParentId == id || (input.ParentId > 0 && IsDescendant(input.ParentId, id)))
            throw new PlatformException("A group cannot be moved under itself or its descendants");

        Apply(group, input);
        group.Update();

        return group;
    }

    /// <summary>删除分组。子分组上移到父级，设备移到父级分组</summary>
    public void Delete(Int32 id)
    {
        var group = Get(id);

        using var tran = DeviceGroup.Meta.CreateTrans();
        foreach (var child in DeviceGroup.FindAllByParentId(id))
        {
            child.ParentId = group.ParentId;
            child.Update();
        }
        foreach (var device in Device.FindAllByGroupId(id))
        {
            device.GroupId = group.ParentId;
            device.Update();
        }
        group.Delete();
        tran.Commit();
    }

    /// <summary>把设备移入分组，groupId=0 表示移出分组</summary>
    public Int32 Assign(GroupAssignInput input)
    {
        if (input.GroupId > 0) Get(input.GroupId);

        var count = 0;
        foreach (var id in input.DeviceIds.Distinct())
        {
            var device = Device.FindById(id);
            if (device == null || device.GroupId == input.GroupId) continue;

            device.GroupId = input.GroupId;
            device.Update();
            count++;
        }

        RefreshCounts();
        return count;
    }

    /// <summary>刷新分组直属设备数、在线数</summary>
    public void RefreshCounts()
    {
        var devices = Device.FindAll();
        foreach (var g in DeviceGroup.FindAll())
        {
            g.Devices = devices.Count(e => e.GroupId == g.Id);
            g.Onlines = devices.Count(e => e.GroupId == g.Id && e.Online);
            if ((g as XCode.IEntity).HasDirty) g.SaveAsync(SqliteSettings.DeviceCounterDelayMs);
        }
    }

    private static void Apply(DeviceGroup group, GroupInput input)
    {
        if (input.ParentId > 0 && DeviceGroup.FindById(input.ParentId) == null) throw PlatformException.NotFound("Parent group");

        var name = input.Name.Trim();
        var exist = DeviceGroup.FindByParentIdAndName(input.ParentId, name);
        if (exist != null && exist.Id != group.Id) throw new PlatformException(409, "Group '{0}' already exists at this level", name);

        group.Name = name;
        group.ParentId = input.ParentId;
        group.Sort = input.Sort;
        group.Enable = input.Enable;
        group.Remark = input.Remark;
    }
}
