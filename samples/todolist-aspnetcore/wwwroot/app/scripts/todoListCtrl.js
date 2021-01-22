'use strict';
angular.module('todoApp')
.controller('todoListCtrl', ['$scope', '$location', 'todoListSvc', function ($scope, $location, todoListSvc) {
    $scope.error = "";
    $scope.loadingMessage = "Loading...";
    $scope.todoList = null;
    $scope.editingInProgress = false;
    $scope.newToDoName = "";


    $scope.editInProgressTodo = {
        name: "",
        isComplete: false,
        id: 0
    };

    

    $scope.editSwitch = function (todo) {
        todo.edit = !todo.edit;
        if (todo.edit) {
            $scope.editInProgressTodo.name = todo.name;
            $scope.editInProgressTodo.id = todo.id;
            $scope.editInProgressTodo.isComplete = todo.isComplete;
            $scope.editingInProgress = true;
        } else {
            $scope.editingInProgress = false;
        }
    };

    $scope.populate = function () {
        todoListSvc.getItems().success(function (results) {
            $scope.todoList = results;
            $scope.loadingMessage = "";
        }).error(function (err) {
            $scope.error = err;
            $scope.loadingMessage = "";
        })
    };
    $scope.delete = function (id) {
        todoListSvc.deleteItem(id).success(function (results) {
            $scope.loadingMessage = "";
            $scope.populate();
        }).error(function (err) {
            $scope.error = err;
            $scope.loadingMessage = "";
        })
    };
    $scope.update = function (todo) {
        $scope.editInProgressTodo.isComplete = todo.isComplete;
        todoListSvc.putItem($scope.editInProgressTodo).success(function (results) {
            $scope.loadingMsg = "";
            $scope.populate();
            $scope.editSwitch(todo);
        }).error(function (err) {
            $scope.error = err;
            $scope.loadingMessage = "";
        })
    };
    $scope.add = function () {
        if ($scope.editingInProgress) {
            $scope.editingInProgress = false;
        }
        todoListSvc.postItem({
            'Name': $scope.newToDoName,
            'IsComplete': false
        }).success(function (results) {
            $scope.loadingMsg = "";
            $scope.newToDoName = "";
            $scope.populate();
        }).error(function (err) {
            $scope.error = err;
            $scope.loadingMsg = "";
        })
    };

}]);
