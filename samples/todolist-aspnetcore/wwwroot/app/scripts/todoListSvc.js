'use strict';
angular.module('todoApp')
.factory('todoListSvc', ['$http', function ($http) {

    $http.defaults.useXDomain = true;
    delete $http.defaults.headers.common['X-Requested-With']; 

    return {
        getItems : function(){
            return $http.get(apiEndpoint + '/api/Todo');
        },
        getItem : function(id){
            return $http.get(apiEndpoint + '/api/Todo/' + id);
        },
        postItem : function(item){
            return $http.post(apiEndpoint + '/api/Todo', item);
        },
        putItem : function(item){
            return $http.put(apiEndpoint + '/api/Todo/' + item.id, item);
        },
        deleteItem : function(id){
            return $http({
                method: 'DELETE',
                url: apiEndpoint + '/api/Todo/' + id
            });
        }
    };
}]);