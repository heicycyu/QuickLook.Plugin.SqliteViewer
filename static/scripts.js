var customKey = '##custom-sql##';

try {
    var app = new Vue({
        el: '#app',
        data: {
            currentNodeName: '',
            treeNodes: [],
            sqlInput: 'select * from xxxxx limit 10',
        },
        mounted: function () {
            that = this;
            var tableNames = JSON.parse(chrome.webview.hostObjects.external.GetTableNames());
            try {
                if (Object.prototype.toString.call(tableNames) === '[object Array]') {
                    var treeNodes = tableNames.map(function (tableName) {
                        return that.genNodeInfo(false, tableName, tableName);
                    });
                    treeNodes.push(that.genNodeInfo(true, customKey, 'SQL 自定义查询'));
                    that.treeNodes = treeNodes;
                    that.sqlInput = 'select * from ' + tableNames[0] + ' limit 10';
                } else {
                    alert('加载表列表失败: 返回的数据不是数组' + '\n\n堆栈信息:\n' + error.stack);
                }
            } catch (error) {
                alert('加载表列表失败: ' + error.message + '\n\n堆栈信息:\n' + error.stack);
            }
        },
        methods: {
            genNodeInfo: function (isCustom, tableName, label) {
                return {
                    isCustom: isCustom,
                    name: tableName,
                    label: label,
                    expanded: isCustom,
                    data: undefined,
                    columns: [],
                    total: 0,
                    currentPage: 1,
                    pageSize: 10,
                };
            },
            /**
             * 切换树节点的展开/折叠状态
             * @param {HTMLElement} treeNode - 树节点元素
             * @param {string} tableName - 表名或 '##custom-sql##' 标识
             */
            toggleNode: function (treeNode, index) {
                var that = this;
                that.currentNodeName = treeNode['name'];
                treeNode['expanded'] = !treeNode['expanded'];

                if (treeNode.isCustom) {
                    return;
                }
                if (index < 0 || index >= this.treeNodes.length) {
                    alert('无效的索引:', index);
                    return;
                }
                if (treeNode.data === undefined) {
                    that.reloadTableData(treeNode, index);
                }
            },
            reloadTableData: function (treeNode, index) {
                that = this;
                treeNode.total = chrome.webview.hostObjects.external.GetTableRecordCount(treeNode.name);
                treeNode.columns = JSON.parse(chrome.webview.hostObjects.external.GetTableColumns(treeNode.name));

                var sqlQuery = "SELECT * FROM " + treeNode.name + " LIMIT " + treeNode.pageSize;
                that.renderTableData(sqlQuery, index);
            },
            renderTableData: function (sqlQuery, index) {
                if (index < 0 || index >= this.treeNodes.length) {
                    alert('无效的索引:', index);
                    return;
                }
                var data = that.getTableData(sqlQuery);
                var columns = [];
                if (data && data.length > 0) {
                    columns = Object.keys(data[0]);
                }
                Vue.set(that.treeNodes[index], "data", data);
                Vue.set(that.treeNodes[index], "columns", columns);
            },
            getTableData: function (sqlQuery) {
                that.showLoading();
                try {
                    var res = JSON.parse(chrome.webview.hostObjects.external.LoadTableData(sqlQuery, false));
                    if (res.status && Array.isArray(res.data)) {
                        return res.data;
                    } else {
                        alert('加载数据失败: ' + (res.message || '返回的数据不是数组'));
                    }
                } catch (error) {
                    alert('加载数据时发生错误: ' + error.message + '\n\n堆栈信息:\n' + error.stack);
                } finally {
                    that.hideLoading();
                }
                return [];
            },
            getRenderData: function (treeNode) {
                if (treeNode.isCustom) {
                    return treeNode.data;
                }
                return treeNode.data.slice((treeNode.currentPage - 1) * treeNode.pageSize, treeNode.currentPage * treeNode.pageSize);
            },
            renderPage: function (treeNode, index, page) {
                treeNode.currentPage = page;
                var sqlQuery = "SELECT * FROM " + treeNode.name + " LIMIT " + treeNode.pageSize + " OFFSET " + (treeNode.currentPage - 1) * treeNode.pageSize;
                that.renderTableData(sqlQuery, index);
            },
            changePageSize: function (treeNode, index, pageSize) {
                treeNode.currentPage = 1;
                treeNode.pageSize = pageSize;
                // this.set(this.treeNodes[index], "currentPage", 1);
                // this.set(this.treeNodes[index], "pageSize", pageSize);
                var sqlQuery = "SELECT * FROM " + treeNode.name + " LIMIT " + treeNode.pageSize + " OFFSET " + (treeNode.currentPage - 1) * treeNode.pageSize;
                that.renderTableData(sqlQuery, index);
            },


            /**
            * 计算需要显示的页码范围
            * @param {Object} node - 分页数据对象
            * @returns {Array} - 包含页码或省略号的数组
            */
            visiblePages: function (node) {
                const totalPages = Math.ceil(node.total / node.pageSize); // 总页数
                const currentPage = node.currentPage; // 当前页
                const range = [];

                if (totalPages <= 5) {
                    // 如果总页数小于等于5，直接显示所有页码
                    for (let i = 1; i <= totalPages; i++) {
                        range.push(i);
                    }
                } else {
                    // 否则，显示当前页及前后两页
                    let start = Math.max(1, currentPage - 2);
                    let end = Math.min(totalPages, currentPage + 2);

                    if (start > 1) {
                        range.push(1); // 始终显示第一页
                        if (start > 2) {
                            range.push('...'); // 添加省略号
                        }
                    }

                    for (let i = start; i <= end; i++) {
                        range.push(i);
                    }

                    if (end < totalPages) {
                        if (end < totalPages - 1) {
                            range.push('...'); // 添加省略号
                        }
                        range.push(totalPages); // 始终显示最后一页
                    }
                }

                return range;
            },



            showLoading: function () {
                // document.getElementById('loading').style.display = 'block';
            },
            hideLoading: function () {
                // document.getElementById('loading').style.display = 'none';
            }
        },
    });
}
catch (error) {
    alert('加载表列表失败: ' + error.message + '\n\n堆栈信息:\n' + error.stack);
}
